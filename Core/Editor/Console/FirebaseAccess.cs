using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Firebase.Editor {
    internal enum FirebaseAccessMode {
        /// <summary>The Firebase CLI, signed in with the developer's Google account.</summary>
        [InspectorName("Firebase CLI")]
        FirebaseCli
      , /// <summary>A service account key file, for machines without the CLI or a Google sign-in.</summary>
        ServiceAccount
    }

    /// <summary>
    /// How this machine's Editor tools reach the Firebase project.<br/>
    /// Kept per user in UserSettings, so a key path or a sign-in never lands in the repository.
    /// </summary>
    internal static class FirebaseAccess {
        private const string ModeKey = "RiseOn.Firebase.AccessMode";
        private const string KeyKey  = "RiseOn.Firebase.ServiceAccountKey";

        public static FirebaseAccessMode Mode {
            get => EditorUserSettings.GetConfigValue(ModeKey) == nameof(FirebaseAccessMode.ServiceAccount) ? FirebaseAccessMode.ServiceAccount : FirebaseAccessMode.FirebaseCli;
            set => EditorUserSettings.SetConfigValue(ModeKey, value.ToString());
        }

        /// <summary>Absolute path of the key file; keep the file outside the project.</summary>
        public static string ServiceAccountKeyPath {
            get => EditorUserSettings.GetConfigValue(KeyKey) ?? "";
            set => EditorUserSettings.SetConfigValue(KeyKey, value);
        }

        private static (string path, DateTime written, ServiceAccountKey key, Exception error) loaded;

        /// <summary>True while the Firebase CLI's status is being checked; a page showing <see cref="Describe"/> spins beside it then.</summary>
        public static bool IsChecking => Mode is FirebaseAccessMode.FirebaseCli && FirebaseCli.IsRefreshingStatus;

        /// <summary>
        /// One line on who the tools act as, for settings pages drawn every frame; null when they cannot act yet.<br/>
        /// Asks the CLI the first time.
        /// </summary>
        public static string Describe(out string problem) {
            problem = null;
            switch (Mode) {
                case FirebaseAccessMode.FirebaseCli:
                    var status = FirebaseCli.LastStatus;
                    if (status == null) {
                        if (!FirebaseCli.IsRefreshingStatus) FirebaseCli.RefreshStatusAsync().Forget();
                        problem = "Checking the Firebase CLI";
                        return null;
                    }

                    if (!status.IsInstalled) problem = "The Firebase CLI is not installed: npm install -g firebase-tools";
                    else if (!status.IsSignedIn) problem = "The Firebase CLI is not signed in.";
                    return status.IsSignedIn ? $"{status.Account} (Firebase CLI)" : null;
                case FirebaseAccessMode.ServiceAccount:
                    try {
                        return $"{LoadServiceAccountCached().ClientEmail} (service account)";
                    } catch (Exception e) {
                        problem = string.IsNullOrEmpty(ServiceAccountKeyPath) ? "No service account key file." : e.Message;
                        return null;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>The configured key; throws when the path is empty or the file is not a service account key.</summary>
        public static ServiceAccountKey LoadServiceAccount() => ServiceAccountKey.Load(ServiceAccountKeyPath);

        // For GUI drawn every frame: reads the file again only when the path or the file changes.
        internal static ServiceAccountKey LoadServiceAccountCached() {
            var path    = ServiceAccountKeyPath;
            var written = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default;
            if (loaded.path != path || loaded.written != written) {
                try {
                    loaded = (path, written, ServiceAccountKey.Load(path), null);
                } catch (Exception e) {
                    loaded = (path, written, null, e);
                }
            }

            if (loaded.error != null) throw loaded.error;
            return loaded.key;
        }
    }
}