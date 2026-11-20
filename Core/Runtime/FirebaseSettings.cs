using Firebase;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Firebase {
    internal enum AppCheckIOSProvider {
        AppAttest
      , DeviceCheck
    }

    /// <summary>
    /// The project's Firebase settings, edited in Project Settings > RiseOn > Firebase and kept in a Resources asset; <see cref="FirebaseInit"/> applies them before anything else touches Firebase.
    /// </summary>
    internal sealed class FirebaseSettings : ScriptableObject {
        public const string ResourceName = "RiseOnFirebaseSettings";

        [SerializeField]
        [Tooltip("How much the Firebase SDK logs. Info is the SDK's own default.")]
        private LogLevel logLevel = LogLevel.Info;

        [SerializeField]
        [Tooltip("Attest the app with App Check (needs the com.google.firebase.app-check package). Enforce it in the Firebase console only once the builds that send tokens are out.")]
        private bool appCheck;

        [SerializeField, LabelText("iOS Provider")]
        [Tooltip("App Attest needs iOS 14 and the App Attest capability; DeviceCheck works on every iOS version the SDK supports.")]
        private AppCheckIOSProvider appCheckIOSProvider = AppCheckIOSProvider.AppAttest;

        [SerializeField, LabelText("Debug in Dev Builds")]
        [Tooltip("Development builds use the debug provider: each device logs a debug token to register in the Firebase console. The Editor always uses it.")]
        private bool appCheckDebugInDevelopmentBuilds = true;

        public LogLevel LogLevel => logLevel;
        public bool AppCheck => appCheck;
        public AppCheckIOSProvider AppCheckIOSProvider => appCheckIOSProvider;
        public bool AppCheckDebugInDevelopmentBuilds => appCheckDebugInDevelopmentBuilds;

        /// <summary>The project's asset, or the defaults when the project never saved one.</summary>
        public static FirebaseSettings Load() {
            var asset = Resources.Load<FirebaseSettings>(ResourceName);
            return asset ? asset : CreateInstance<FirebaseSettings>();
        }
    }
}