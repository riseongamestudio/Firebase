using System;
using Cysharp.Threading.Tasks;
using Firebase;
using UnityEngine;
#if RISEON_FIREBASE_APP_CHECK
using Firebase.AppCheck;
#endif

namespace RiseOn.Firebase {
    /// <summary>
    /// Starts Firebase once per launch on its own, before the first scene loads: applies <see cref="FirebaseSettings"/> (App Check first, as it must come before any other Firebase call), checks Google Play services and asks the player to fix them, then creates the default app.<br/>
    /// Every package that uses Firebase awaits <see cref="WaitUntilCompleteAsync"/>; nothing has to start it.<br/>
    /// If it fails, it waits for the one caller of <see cref="PopResultAsync"/> to retry; a game that uses Firebase does not run without it.
    /// </summary>
    public static class FirebaseInit {
        private static UniTaskCompletionSource<FirebaseApp> completion = new();
        private static Attempt                              current    = new();
        private static bool                                 configured;

        /// <summary>
        /// Completes on Unity's thread with the default app once the startup completes, at once if it has.<br/>
        /// Never fails.
        /// </summary>
        public static UniTask<FirebaseApp> WaitUntilCompleteAsync() {
            return completion.Task;
        }

        /// <summary>
        /// The result of the current run, for the one caller that handles a failure; a second call for the same run throws, so a failure never reaches two handlers.
        /// </summary>
        public static UniTask<FirebaseInitResult> PopResultAsync() {
            if (current.Popped) throw new InvalidOperationException($"{nameof(FirebaseInit)}: the result of this run was already popped");

            current.Popped = true;
            return current.Result.Task;
        }

        // Static state outlives a play session when the Editor skips the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            completion = new UniTaskCompletionSource<FirebaseApp>();
            current    = new Attempt();
            configured = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Run() {
            RunAsync(current).Forget();
        }

        private static async UniTaskVoid RunAsync(Attempt attempt) {
            attempt.Result.TrySetResult(await AttemptAsync());
        }

        // Runs on its own up to the first await, so the settings still apply before the first scene loads.
        private static async UniTask<FirebaseInitResult> AttemptAsync() {
            // A run that could not apply them tries again, still before any other Firebase call.
            if (!configured) {
                try {
                    var settings = FirebaseSettings.Load();
                    FirebaseApp.LogLevel = settings.LogLevel;
                    SetUpAppCheck(settings);
                } catch (Exception e) {
                    return Fail(new FirebaseInitException(DependencyStatus.UnavailableOther, "the settings could not be applied", e));
                }

                configured = true;
            }

            DependencyStatus status;
            try {
                status = await FirebaseApp.CheckAndFixDependenciesAsync();
            } catch (Exception e) {
                return Fail(new FirebaseInitException(DependencyStatus.UnavailableOther, "the dependency check threw", e));
            }

            await UniTask.SwitchToMainThread();
            // Not only Google Play services: an Android build without google-services.json also gets UnavailableInvalid.
            if (status is not DependencyStatus.Available) return Fail(new FirebaseInitException(status, $"the dependency check returned {status}"));

            FirebaseApp app;
            try {
                app = FirebaseApp.DefaultInstance;
            } catch (Exception e) {
                return Fail(new FirebaseInitException(status, "the default app could not be created", e));
            }

            Debug.Log($"{nameof(FirebaseInit)}: ready ({app.Options.ProjectId})");
            completion.TrySetResult(app);
            return new FirebaseInitSucceeded();
        }

        private static FirebaseInitResult Fail(FirebaseInitException error) {
            Debug.LogError($"{nameof(FirebaseInit)}: {error.Message}{(error.InnerException != null ? $" ({error.InnerException.Message})" : "")}; waiting for a retry");
            return new FirebaseInitFailed(error, RetryAsync);
        }

        // The retrying caller owns the new run, so nobody can pop it.
        private static UniTask<FirebaseInitResult> RetryAsync() {
            current = new Attempt { Popped = true };
            RunAsync(current).Forget();
            return current.Result.Task;
        }

        private static void SetUpAppCheck(FirebaseSettings settings) {
            if (!settings.AppCheck) return;

#if RISEON_FIREBASE_APP_CHECK
            var factory = AppCheckFactory(settings);
            if (factory == null) {
                Debug.LogWarning($"{nameof(FirebaseInit)}: App Check has no provider on {Application.platform}");
                return;
            }

            FirebaseAppCheck.SetAppCheckProviderFactory(factory);
#else
            Debug.LogError($"{nameof(FirebaseInit)}: App Check is on but com.google.firebase.app-check is not installed");
#endif
        }

#if RISEON_FIREBASE_APP_CHECK
        private static IAppCheckProviderFactory AppCheckFactory(FirebaseSettings settings) {
            if (Application.isEditor) return EditorDebugFactory();
            if (Debug.isDebugBuild && settings.AppCheckDebugInDevelopmentBuilds) return DebugAppCheckProviderFactory.Instance;

            switch (Application.platform) {
                case RuntimePlatform.Android:
                    return PlayIntegrityProviderFactory.Instance;
                case RuntimePlatform.IPhonePlayer:
                    switch (settings.AppCheckIOSProvider) {
                        // Firebase's own setup falls back to DeviceCheck before iOS 14, which has no App Attest.
                        case AppCheckIOSProvider.AppAttest:   return IOSMajorVersion() >= 14 ? AppAttestProviderFactory.Instance : DeviceCheckProviderFactory.Instance;
                        case AppCheckIOSProvider.DeviceCheck: return DeviceCheckProviderFactory.Instance;
                        default:                              throw new ArgumentOutOfRangeException(nameof(settings.AppCheckIOSProvider), settings.AppCheckIOSProvider, null);
                    }
                default:
                    return null;
            }
        }

        // The desktop debug provider makes no token of its own: it takes one made in the Firebase console, which each
        // developer keeps in their UserSettings so it never ships.
        private static IAppCheckProviderFactory EditorDebugFactory() {
#if UNITY_EDITOR
            var token = UnityEditor.EditorUserSettings.GetConfigValue(AppCheckEditorTokenKey);
            if (string.IsNullOrEmpty(token)) {
                Debug.LogWarning($"{nameof(FirebaseInit)}: App Check in the Editor needs a debug token; add one in Project Settings > RiseOn > Firebase");
            } else {
                DebugAppCheckProviderFactory.Instance.SetDebugToken(token);
            }
#endif
            return DebugAppCheckProviderFactory.Instance;
        }

        // SystemInfo, not UnityEngine.iOS.Device, which the Android player does not have; it reads like "iOS 17.4.1".
        private static int IOSMajorVersion() {
            var text  = SystemInfo.operatingSystem;
            var start = 0;
            while (start < text.Length && !char.IsDigit(text[start])) start++;

            var end = start;
            while (end < text.Length && char.IsDigit(text[end])) end++;
            return int.TryParse(text.Substring(start, end - start), out var major) ? major : 0;
        }
#endif

        /// <summary>The EditorUserSettings key of the App Check debug token the Editor uses.</summary>
        internal const string AppCheckEditorTokenKey = "RiseOn.Firebase.AppCheckDebugToken";

        private sealed class Attempt {
            public readonly UniTaskCompletionSource<FirebaseInitResult> Result = new();
            public          bool                                        Popped;
        }
    }
}