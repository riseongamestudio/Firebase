using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Google.MiniJSON;
using UnityEditor;
using UnityEditor.Build;

namespace RiseOn.Firebase.Editor {
    /// <summary>One platform's Firebase config file, as the Firebase Unity SDK finds it: anywhere under Assets.</summary>
    internal sealed class FirebaseConfigFile {
        /// <summary>Asset path, or null when the project has none.</summary>
        public string Path { get; }

        /// <summary>Every file of this name under Assets; with several, Firebase picks one by rules of its own.</summary>
        public IReadOnlyList<string> Paths { get; }

        public string ProjectId { get; }

        /// <summary>Android package names (one per app in the file) or the iOS bundle id.</summary>
        public IReadOnlyList<string> AppIds { get; }

        /// <summary>Why the file could not be read; null when it was.</summary>
        public string Error { get; }

        public bool Exists => Path != null;

        internal FirebaseConfigFile(IReadOnlyList<string> paths, string projectId, IReadOnlyList<string> appIds, string error) {
            Paths     = paths;
            Path      = paths.FirstOrDefault();
            ProjectId = projectId;
            AppIds    = appIds ?? Array.Empty<string>();
            Error     = error;
        }
    }

    /// <summary>Reads google-services.json and GoogleService-Info.plist, which say which Firebase project the game uses.</summary>
    internal static class FirebaseProjectFiles {
        public const string AndroidFileName = "google-services.json";
        public const string IOSFileName     = "GoogleService-Info.plist";

        private static FirebaseConfigFile android;
        private static FirebaseConfigFile ios;

        public static FirebaseConfigFile Android => android ??= ReadAndroid(Find(AndroidFileName));
        public static FirebaseConfigFile IOS => ios ??= ReadIOS(Find(IOSFileName));

        // Read again after any asset change, so the settings pages can show them every frame.
        [InitializeOnLoadMethod]
        private static void ForgetOnProjectChange() {
            EditorApplication.projectChanged += () => {
                android = null;
                ios     = null;
            };
        }

        /// <summary>The project both files name, or the one file there is; null when there is none or they differ.</summary>
        public static string ProjectId {
            get {
                var ids = new[] { Android.ProjectId, IOS.ProjectId }.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();
                return ids.Length == 1 ? ids[0] : null;
            }
        }

        /// <summary>Where a config file problem is fixed, for the page's button.</summary>
        public enum ProblemFix { None, Console, PlayerSettings }

        /// <summary>
        /// What is wrong with the config file a build for <paramref name="target"/> ships: missing, more than one, unreadable, made for another app id than Player Settings builds, or naming another project than the other platform's file.
        /// </summary>
        public static IEnumerable<(string Message, ProblemFix Fix)> Problems(BuildTarget target) {
            var (file, other, name, platform) = target is BuildTarget.iOS ? (IOS, Android, IOSFileName, NamedBuildTarget.iOS) : (Android, IOS, AndroidFileName, NamedBuildTarget.Android);
            if (!file.Exists) {
                yield return ($"No {name} under Assets. Download it from the Firebase console, Project settings > Your apps.", ProblemFix.Console);
                yield break;
            }

            // The SDK picks among several by app id and path order, and the Android and iOS steps by different rules.
            if (file.Paths.Count > 1) {
                yield return ($"{file.Paths.Count} {name} files under Assets ({string.Join(", ", file.Paths)}); keep one.", ProblemFix.None);
                yield break;
            }

            if (file.Error != null) {
                yield return ($"{file.Path} could not be read ({file.Error}).", ProblemFix.None);
                yield break;
            }

            var applicationId = PlayerSettings.GetApplicationIdentifier(platform);
            if (!file.AppIds.Contains(applicationId)) yield return ($"{file.Path} is for {string.Join(", ", file.AppIds)}, but Player Settings build {applicationId}.", ProblemFix.PlayerSettings);
            if (other.ProjectId != null && other.ProjectId != file.ProjectId) yield return ($"{file.Path} names project {file.ProjectId}, {other.Path} names {other.ProjectId}.", ProblemFix.None);
        }

        private static string[] Find(string fileName) {
            var name = System.IO.Path.GetFileNameWithoutExtension(fileName);
            return AssetDatabase.FindAssets(name)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) && System.IO.Path.GetFileName(path) == fileName)
                .Distinct()
                .ToArray();
        }

        private static FirebaseConfigFile ReadAndroid(string[] paths) {
            if (paths.Length != 1) return new FirebaseConfigFile(paths, null, null, null);

            var path = paths[0];

            try {
                var root      = (Dictionary<string, object>)Json.Deserialize(File.ReadAllText(path));
                var info      = (Dictionary<string, object>)root["project_info"];
                var projectId = (string)info["project_id"];
                var packages  = ((List<object>)root["client"])
                    .Cast<Dictionary<string, object>>()
                    .Select(client => (string)((Dictionary<string, object>)((Dictionary<string, object>)client["client_info"])["android_client_info"])["package_name"])
                    .ToArray();
                return new FirebaseConfigFile(paths, projectId, packages, null);
            } catch (Exception e) {
                return new FirebaseConfigFile(paths, null, null, e.Message);
            }
        }

        private static FirebaseConfigFile ReadIOS(string[] paths) {
            if (paths.Length != 1) return new FirebaseConfigFile(paths, null, null, null);

            var path = paths[0];

            try {
                var dict   = XDocument.Load(path).Root!.Element("dict")!.Elements().ToArray();
                var values = new Dictionary<string, string>();
                for (var i = 0; i + 1 < dict.Length; i += 2) values[dict[i].Value] = dict[i + 1].Value;

                values.TryGetValue("PROJECT_ID", out var projectId);
                values.TryGetValue("BUNDLE_ID", out var bundleId);
                return new FirebaseConfigFile(paths, projectId, bundleId != null ? new[] { bundleId } : null, null);
            } catch (Exception e) {
                return new FirebaseConfigFile(paths, null, null, e.Message);
            }
        }
    }
}