using System;
using System.IO;
using Cysharp.Threading.Tasks;
using RiseOn.Utils.Editor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace RiseOn.Firebase.Editor {
    /// <summary>
    /// Project Settings > RiseOn > Firebase: the config files the game ships, the settings the startup applies, and how this machine's Editor tools reach the project.
    /// </summary>
    internal static class FirebaseSettingsProvider {
        public const   string PagePath        = "Project/RiseOn/Firebase";
        internal const string AppCheckPackage = "com.google.firebase.app-check";

        private static readonly SettingsTree tree = new();

#if !RISEON_FIREBASE_APP_CHECK
        private static string appCheckAdding;
#endif

        [MenuItem("RiseOn/Firebase/Settings", priority = 0)]
        internal static void Open() {
            SettingsService.OpenProjectSettings(PagePath);
        }

        [SettingsProvider]
        private static SettingsProvider Create() =>
            new(PagePath, SettingsScope.Project) {
                guiHandler = _ => {
                    using var page = SettingsPage.Begin();
                    Draw(LoadOrCreate());
                }
              , deactivateHandler = tree.Dispose
              , keywords = new[] { "firebase", "google-services", "GoogleService-Info", "App Check", "log", "console", "service account", "firebase-tools" }
            };

        /// <summary>The whole page for one settings asset.</summary>
        internal static void Draw(FirebaseSettings settings) {
            // The same problems stop the build, for that platform where they name one.
            if (OutsideResources() is { } outside && HelpBoxUtils.Draw(outside, MessageType.Error, new GUIContent("Move it"))) {
                SettingsAssetUtils.MoveToResources<FirebaseSettings>(FirebaseSettings.ResourceName);
            }

            foreach (var problem in FirebaseProjectFiles.Problems(BuildTarget.Android)) HelpBoxUtils.Draw($"Android: {problem}", MessageType.Error);
            foreach (var problem in FirebaseProjectFiles.Problems(BuildTarget.iOS)) HelpBoxUtils.Draw($"iOS: {problem}", MessageType.Error);

            using (new Section("Config files", "google-services.json and GoogleService-Info.plist, from the Firebase console > Project settings.")) {
                DrawConfigFile("Android", FirebaseProjectFiles.Android);
                DrawConfigFile("iOS", FirebaseProjectFiles.IOS);
            }

            var root = tree.Begin(settings);
            using (new Section("Startup", "Applied before anything else touches Firebase.")) {
                root.Children["logLevel"].Draw();

                // App Check's own settings, open only while it is on.
                var appCheck = root.Children["appCheck"];
                // The tree is made again as the page reopens; the fold is the session's, which the section keeps each draw.
                Section.Restore(appCheck);
                using var section = Section.Foldout(appCheck, "App Check", appCheck.Info.GetAttribute<TooltipAttribute>()?.tooltip, toggle: appCheck);
                if (section.Open) {
                    root.Children["appCheckIOSProvider"].Draw();
                    root.Children["appCheckDebugInDevelopmentBuilds"].Draw();
                    DrawAppCheckPackage();
                }
            }

            tree.End();

            using (new Section("Console access", "How Editor tools on this machine reach the project in the Firebase console, as Remote Config's Pull and Push do. Saved for you only, in UserSettings.")) {
                DrawAccess(FirebaseProjectFiles.ProjectId);
            }

        }

        /// <summary>The project's settings asset, made with the defaults the first time the page opens.</summary>
        internal static FirebaseSettings LoadOrCreate() => SettingsAssetUtils.LoadOrCreate<FirebaseSettings>(FirebaseSettings.ResourceName);

        /// <summary>For the build steps: the asset wherever the project keeps it, or the defaults.</summary>
        internal static FirebaseSettings Find() {
            var settings = SettingsAssetUtils.Find<FirebaseSettings>(FirebaseSettings.ResourceName);
            return settings ? settings : ScriptableObject.CreateInstance<FirebaseSettings>();
        }

        /// <summary>Why the game cannot load the asset, which it reads from Resources: null when it can, or when there is none and both run with the defaults.</summary>
        internal static string OutsideResources() =>
            SettingsAssetUtils.OutsideResources<FirebaseSettings>(FirebaseSettings.ResourceName) is { } path ? $"{path} is outside a Resources folder, so the game cannot load it and runs with the defaults." : null;

        // A file with problems shows them at the top of the page instead.
        private static void DrawConfigFile(string platform, FirebaseConfigFile file) {
            if (!file.Exists || file.Paths.Count > 1 || file.Error != null) {
                EditorGUILayout.LabelField(platform, "None");
                return;
            }

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(platform, $"{file.ProjectId}  ({file.Path})");
                if (GUILayout.Button("Show", GUILayout.Width(50))) EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(file.Path));
            }
        }

        private static void DrawAccess(string projectId) {
            ChoiceFieldUtils.Enum(new GUIContent("Access"), FirebaseAccess.Mode, picked => FirebaseAccess.Mode = picked);
            switch (FirebaseAccess.Mode) {
                case FirebaseAccessMode.FirebaseCli:
                    DrawCli();
                    break;
                case FirebaseAccessMode.ServiceAccount:
                    DrawServiceAccount(projectId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void DrawCli() {
            var status   = FirebaseCli.LastStatus;
            var checking = FirebaseCli.IsRefreshingStatus;
            if (status == null && !checking) RefreshCliAsync().Forget();

            var text = status == null          ? "Checking firebase-tools"
                     : !status.IsInstalled    ? "Not installed: npm install -g firebase-tools"
                     : !status.IsSignedIn     ? $"firebase-tools {status.Version}, not signed in"
                     : $"{status.Account}  (firebase-tools {status.Version})";

            // One control rect cut into the label, the text, the button and the reload, so the row is one line tall as the page's other rows;
            // controls laid out side by side each add their own margins and stand past the section's border.
            var line    = EditorGUILayout.GetControlRect();
            var refresh = new Rect(line.xMax - Section.ButtonWidth, line.y, Section.ButtonWidth, line.height);
            var button  = new Rect(refresh.x - Section.LabelGap - 110, line.y, 110, line.height);
            var value   = EditorGUI.PrefixLabel(new Rect(line.x, line.y, button.x - Section.LabelGap - line.x, line.height), new GUIContent("Google account"));
            // A check under way spins beside what the last one found.
            EditorGUIUtility.SetIconSize(new Vector2(Section.IconSize, Section.IconSize));
            GUI.Label(value, checking ? SpinnerUtils.Content(text) : new GUIContent(text));
            EditorGUIUtility.SetIconSize(Vector2.zero);

            // Off while checking or without firebase-tools; the tooltips say so.
            var signIn = checking                         ? "Sign in is off.\nfirebase-tools is being checked."
                       : status is not { IsInstalled: true } ? "Sign in is off.\nInstall firebase-tools first: npm install -g firebase-tools"
                       : "Signs firebase-tools in to a Google account, in the browser";
            using (new EditorGUI.DisabledScope(checking || status is not { IsInstalled: true })) {
                if (GUI.Button(button, new GUIContent(status is { IsSignedIn: true } ? "Switch account" : "Sign in", signIn))) FirebaseCli.OpenSignIn();
            }

            using (new EditorGUI.DisabledScope(checking)) {
                if (Section.IconButton(refresh, SdfIconType.ArrowClockwise, checking ? "Checking firebase-tools now" : "Check firebase-tools and its account again")) RefreshCliAsync().Forget();
            }
        }

        private static async UniTaskVoid RefreshCliAsync() {
            try {
                await FirebaseCli.RefreshStatusAsync();
            } finally {
                SettingsService.RepaintAllSettingsWindow();
            }
        }

        private static void DrawServiceAccount(string projectId) {
            using (new EditorGUILayout.HorizontalScope()) {
                FirebaseAccess.ServiceAccountKeyPath = EditorGUILayout.DelayedTextField("Key file", FirebaseAccess.ServiceAccountKeyPath);
                if (GUILayout.Button("Browse", GUILayout.Width(70))) {
                    var path = EditorUtility.OpenFilePanel("Service account key", "", "json");
                    if (!string.IsNullOrEmpty(path)) FirebaseAccess.ServiceAccountKeyPath = path;
                }
            }

            if (string.IsNullOrEmpty(FirebaseAccess.ServiceAccountKeyPath)) {
                HelpBoxUtils.Draw("Firebase console > Project settings > Service accounts > Generate new private key. Keep the file outside the project: it grants admin access.", MessageType.Info);
                return;
            }

            try {
                var key = FirebaseAccess.LoadServiceAccountCached();
                EditorGUILayout.LabelField("Account", key.ClientEmail);

                if (projectId != null && key.ProjectId != projectId) HelpBoxUtils.Draw($"The key is for {key.ProjectId}, the game uses {projectId}.", MessageType.Warning);
                if (Path.GetFullPath(key.Path).StartsWith(Path.GetFullPath("."), StringComparison.OrdinalIgnoreCase)) {
                    HelpBoxUtils.Draw("The key file is inside the project and may be committed. Move it out.", MessageType.Warning);
                }
            } catch (Exception e) {
                HelpBoxUtils.Draw(e.Message, MessageType.Error);
            }
        }

        private static void DrawAppCheckPackage() {
#if RISEON_FIREBASE_APP_CHECK
            var token = EditorUserSettings.GetConfigValue(FirebaseInit.AppCheckEditorTokenKey) ?? "";
            EditorGUI.BeginChangeCheck();
            token = EditorGUILayout.DelayedTextField(new GUIContent("Editor Debug Token", "A debug token made in the Firebase console, App Check > Apps > Manage debug tokens. Kept for you only, in UserSettings."), token);
            if (EditorGUI.EndChangeCheck()) EditorUserSettings.SetConfigValue(FirebaseInit.AppCheckEditorTokenKey, token.Trim());
            if (token.Length == 0) HelpBoxUtils.Draw("Play mode sends no valid App Check token without one; enforced services then refuse the Editor.", MessageType.Info);
#else
            // The same box as the other pages' issues with a fix, an error as it stops the build; the button spins while it installs.
            var button = appCheckAdding == null ? new GUIContent("Install") : SpinnerUtils.Content(appCheckAdding);
            if (HelpBoxUtils.Draw($"App Check needs {AppCheckPackage}, from the same registry as Firebase.", MessageType.Error, button, appCheckAdding == null)) AddAppCheckAsync().Forget();
#endif
        }

#if !RISEON_FIREBASE_APP_CHECK
        // App Check ships with the Firebase SDK in lockstep, so it takes the installed app's version.
        private static async UniTaskVoid AddAppCheckAsync() {
            appCheckAdding = "Installing";
            try {
                var app = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.google.firebase.app/package.json");
                var request = Client.Add(app != null ? $"{AppCheckPackage}@{app.version}" : AppCheckPackage);
                await UniTask.WaitUntil(() => request.IsCompleted);
                if (request.Status is StatusCode.Failure) Debug.LogError($"{nameof(FirebaseSettingsProvider)}: {AppCheckPackage} could not be added ({request.Error.message})");
            } finally {
                appCheckAdding = null;
            }
        }
#endif
    }

    [CustomEditor(typeof(FirebaseSettings))]
    internal sealed class FirebaseSettingsInspector : SettingsAssetInspector {
        protected override string PagePath => FirebaseSettingsProvider.PagePath;
    }
}