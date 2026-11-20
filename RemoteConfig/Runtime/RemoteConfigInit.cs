using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Firebase.RemoteConfig;
using UnityEngine;

namespace RiseOn.Firebase.RemoteConfig {
    /// <summary>
    /// The launch load, run once on its own after <see cref="FirebaseInit"/>: sets the in-app defaults and the fetch settings of <see cref="FirebaseRemoteConfigSettings"/>, activates (after a fetch, or what the last launch fetched, as <see cref="FirebaseRemoteConfigSettings.Loading"/> says), and gives every <see cref="RemoteValue{T}"/> its value of launch; live parameters then follow what is published.<br/>
    /// Code that needs the values of launch awaits <see cref="WaitUntilCompleteAsync"/>.<br/>
    /// When loading fails, it waits for the one caller of <see cref="PopResultAsync"/> to retry, or to continue with the values the device has fetched and the in-app defaults for the rest.
    /// </summary>
    public static class RemoteConfigInit {
        private static UniTaskCompletionSource completion = new();
        private static Attempt                 current    = new();
        private static bool                    configured;
        private static FirebaseRemoteConfig    listening;

        /// <summary>Completes on Unity's thread once every value has its value of launch, at once if it has; never fails.</summary>
        public static UniTask WaitUntilCompleteAsync() {
            return completion.Task;
        }

        /// <summary>
        /// The result of the current run, for the one caller that handles a failure; a second call for the same run throws, so a failure never reaches two handlers.
        /// </summary>
        public static UniTask<RemoteConfigInitResult> PopResultAsync() {
            if (current.Popped) throw new InvalidOperationException($"{nameof(RemoteConfigInit)}: the result of this run was already popped");

            current.Popped = true;
            return current.Result.Task;
        }

        // Static state outlives a play session when the Editor skips the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            completion = new UniTaskCompletionSource();
            current    = new Attempt();
            configured = false;
            if (listening != null) listening.OnConfigUpdateListener -= OnConfigUpdate;
            listening = null;
            RemoteValues.Forget();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Run() {
            RunAsync(current).Forget();
        }

        private static async UniTaskVoid RunAsync(Attempt attempt) {
            await FirebaseInit.WaitUntilCompleteAsync();
            attempt.Result.TrySetResult(await AttemptAsync());
        }

        private static async UniTask<RemoteConfigInitResult> AttemptAsync() {
            FirebaseRemoteConfigSettings settings;
            FirebaseRemoteConfig         config   = null;
            var                          fetching = false;
            try {
                settings = FirebaseRemoteConfigSettings.Load();
                config   = FirebaseRemoteConfig.DefaultInstance;
                if (!configured) {
                    await config.SetDefaultsAsync(RemoteValues.All.ToDictionary(value => value.Key, value => (object)Default(settings, value.Key)));
                    await config.SetConfigSettingsAsync(new ConfigSettings {
                        FetchTimeoutInMilliseconds         = (ulong)settings.FetchTimeoutSeconds * 1000
                      , MinimumFetchIntervalInMilliseconds = (ulong)FetchInterval(settings).TotalMilliseconds
                    });
                    configured = true;
                }

                switch (settings.Loading) {
                    case RemoteConfigLoading.WaitForFetch:
                        fetching = true;
                        await config.FetchAsync(FetchInterval(settings));
                        fetching = false;
                        await config.ActivateAsync();
                        break;
                    case RemoteConfigLoading.NextLaunch:
                        await config.ActivateAsync();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(settings.Loading), settings.Loading, null);
                }
            } catch (Exception e) {
                await UniTask.SwitchToMainThread();
                // The fetch faults with an empty FirebaseException; the reason is in the config info, which holds an earlier
                // fetch's for any other failure.
                var reason = fetching ? config.Info.LastFetchFailureReason : FetchFailureReason.Error;
                var cause  = $"{reason}{(string.IsNullOrEmpty(e.Message) ? "" : ": " + e.Message)}";
                Debug.LogError($"{nameof(RemoteConfigInit)}: loading failed ({cause}); waiting for a retry or continue");
                return new RemoteConfigInitFailed(e, reason, RetryAsync, () => ContinueAsync(config));
            }

            await UniTask.SwitchToMainThread();
            Complete(config);
            if (settings.Loading is RemoteConfigLoading.NextLaunch) FetchForNextLaunchAsync(config, FetchInterval(settings)).Forget();
            return new RemoteConfigInitSucceeded();
        }

        // Nothing waits on it: a failure only means the next launch keeps today's values and fetches again.
        private static async UniTaskVoid FetchForNextLaunchAsync(FirebaseRemoteConfig config, TimeSpan interval) {
            try {
                await config.FetchAsync(interval);
                await UniTask.SwitchToMainThread();
                Debug.Log($"{nameof(RemoteConfigInit)}: fetched for the next launch");
            } catch (Exception) {
                await UniTask.SwitchToMainThread();
                Debug.LogWarning($"{nameof(RemoteConfigInit)}: fetch for the next launch failed ({config.Info.LastFetchFailureReason})");
            }
        }

        // The retrying caller owns the new run, so nobody can pop it.
        private static UniTask<RemoteConfigInitResult> RetryAsync() {
            current = new Attempt { Popped = true };
            RunAsync(current).Forget();
            return current.Result.Task;
        }

        private static async UniTask ContinueAsync(FirebaseRemoteConfig config) {
            // Without an instance every value already holds its in-app default.
            if (config == null) {
                await UniTask.SwitchToMainThread();
                completion.TrySetResult();
                return;
            }

            // A fetch from an earlier launch may wait unactivated.
            try {
                await config.ActivateAsync();
            } catch (Exception e) {
                Debug.LogWarning($"{nameof(RemoteConfigInit)}: activating stored values failed ({e.Message})");
            }

            await UniTask.SwitchToMainThread();
            Complete(config);
        }

        private static void Complete(FirebaseRemoteConfig config) {
            foreach (var value in RemoteValues.All) value.Set(Read(config, value.Key));

            Debug.Log($"{nameof(RemoteConfigInit)}: ready ({config.Info.LastFetchStatus}, fetched {config.Info.FetchTime:u})");
            completion.TrySetResult();

            if (listening == null && RemoteValues.All.Any(value => value.IsLive)) {
                listening = config;
                config.OnConfigUpdateListener += OnConfigUpdate;
            }
        }

        // Firebase raises this off Unity's thread; the keys are copied before switching.
        private static void OnConfigUpdate(object sender, ConfigUpdateEventArgs args) {
            ApplyUpdateAsync(args.Error, args.UpdatedKeys?.ToArray() ?? Array.Empty<string>()).Forget();
        }

        private static async UniTaskVoid ApplyUpdateAsync(RemoteConfigError error, string[] keys) {
            await UniTask.SwitchToMainThread();
            if (listening == null) return;

            if (error is not RemoteConfigError.None) {
                Debug.LogWarning($"{nameof(RemoteConfigInit)}: real-time updates failed ({error})");
                return;
            }

            var updated = new HashSet<string>(keys);
            var live    = RemoteValues.All.Where(value => value.IsLive && updated.Contains(value.Key)).ToArray();
            if (live.Length == 0) return;

            var config = listening;
            try {
                await config.ActivateAsync();
            } catch (Exception e) {
                Debug.LogWarning($"{nameof(RemoteConfigInit)}: activating an update failed ({e.Message})");
                return;
            }

            await UniTask.SwitchToMainThread();
            foreach (var value in live) value.Set(Read(config, value.Key));
        }

        // A key neither the defaults nor a fetch gave reads as a static empty value; null keeps the in-app default.
        private static string Read(FirebaseRemoteConfig config, string key) {
            var value = config.GetValue(key);
            return value.Source is ValueSource.StaticValue ? null : value.StringValue;
        }

        private static string Default(FirebaseRemoteConfigSettings settings, string key) {
            return settings.Parameters.First(parameter => parameter.Key == key).DefaultValue ?? "";
        }

        private static TimeSpan FetchInterval(FirebaseRemoteConfigSettings settings) {
            return TimeSpan.FromMinutes(Debug.isDebugBuild ? settings.DevelopmentMinimumFetchIntervalMinutes : settings.MinimumFetchIntervalMinutes);
        }

        private sealed class Attempt {
            public readonly UniTaskCompletionSource<RemoteConfigInitResult> Result = new();
            public          bool                                        Popped;
        }
    }
}