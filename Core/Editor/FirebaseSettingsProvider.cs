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

#if !FIREBASE_SUPPORT_APPCHECK
        private static string appCheckAdding;
#endif

        [MenuItem("RiseOn/Firebase/Settings", priority = 0)]
        internal static void Open() {
            SettingsService.OpenProjectSettings(PagePath);
        }

        [SettingsProvider]
        private static SettingsProvider Create() {
            return new(PagePath, SettingsScope.Project) {
                guiHandler = _ => {
                    using var page = SettingsPage.Begin();
                    Draw(LoadOrCreate());
                }
              , deactivateHandler = tree.Dispose
              , keywords = new[] { "firebase", "google-services", "GoogleService-Info", "App Check", "log", "console", "service account", "firebase-tools" }
            };
        }

        /// <summary>The whole page for one settings asset.</summary>
        internal static void Draw(FirebaseSettings settings) {
            // The same problems stop the build, for that platform where they name one.
            if (OutsideResources() is { } outside && HelpBoxUtils.Draw(outside, MessageType.Error, new GUIContent("Move it"))) {
                SettingsAssetUtils.MoveToResources<FirebaseSettings>(FirebaseSettings.ResourceName);
            }

            DrawFileProblems("Android", BuildTarget.Android);
            DrawFileProblems("iOS", BuildTarget.iOS);
#if !FIREBASE_SUPPORT_APPCHECK
            // The same box as the other pages' issues with a fix, an error as it stops the build; the button spins while it installs.
            if (settings.AppCheck) {
                var button = appCheckAdding == null ? new GUIContent("Install") : SpinnerUtils.Content(appCheckAdding);
                if (HelpBoxUtils.Draw($"App Check is on and needs {AppCheckPackage}, from the same registry as Firebase.", MessageType.Error, button, appCheckAdding == null)) AddAppCheckAsync().Forget();
            }
#endif

            using (new Section("Config files", "google-services.json and GoogleService-Info.plist, from the Firebase console > Project settings.")) {
                DrawConfigFile("Android", FirebaseProjectFiles.Android, FirebaseProjectFiles.AndroidFileName);
                DrawConfigFile("iOS", FirebaseProjectFiles.IOS, FirebaseProjectFiles.IOSFileName);
            }

            var root = tree.Begin(settings);
            using (new Section("Startup", "Applied before anything else touches Firebase.")) {
                root.Children["logLevel"].Draw();

                // App Check's own settings.
                var appCheck = root.Children["appCheck"];
                // The tree is made again as the page reopens; the fold is the session's, which the section keeps each draw.
                Section.Restore(appCheck);
                using var section = Section.Foldout(appCheck, "App Check", appCheck.Info.GetAttribute<TooltipAttribute>()?.tooltip, toggle: appCheck);
                if (section.Open) {
                    root.Children["appCheckIOSProvider"].Draw();
                    root.Children["appCheckDebugInDevelopmentBuilds"].Draw();
#if FIREBASE_SUPPORT_APPCHECK
                    DrawEditorToken();
#endif
                }
            }

            tree.End();

            using (new Section("Console access", "How Editor tools on this machine reach the project in the Firebase console, as Remote Config's Pull and Push do. Saved for you only, in UserSettings.")) {
                DrawAccess(FirebaseProjectFiles.ProjectId);
            }

        }

        /// <summary>The project's settings asset, made with the defaults the first time the page opens.</summary>
        internal static FirebaseSettings LoadOrCreate() {
            return SettingsAssetUtils.LoadOrCreate<FirebaseSettings>(FirebaseSettings.ResourceName);
        }

        /// <summary>For the build steps: the asset wherever the project keeps it, or the defaults.</summary>
        internal static FirebaseSettings Find() {
            var settings = SettingsAssetUtils.Find<FirebaseSettings>(FirebaseSettings.ResourceName);
            return settings ? settings : ScriptableObject.CreateInstance<FirebaseSettings>();
        }

        /// <summary>Why the game cannot load the asset, which it reads from Resources: null when it can, or when there is none and both run with the defaults.</summary>
        internal static string OutsideResources() {
            return SettingsAssetUtils.OutsideResources<FirebaseSettings>(FirebaseSettings.ResourceName) is { } path ? $"{path} is outside a Resources folder, so the game cannot load it and runs with the defaults." : null;
        }

        // A missing file's box opens the console where it downloads: the project's settings, which list its apps, once the other platform's
        // file names the project; else the console's start, to pick it. A file for another app opens Player Settings, where the app id is.
        private static void DrawFileProblems(string platform, BuildTarget target) {
            foreach (var (message, fix) in FirebaseProjectFiles.Problems(target)) {
                switch (fix) {
                    case FirebaseProjectFiles.ProblemFix.Console:
                        var projectId = FirebaseProjectFiles.ProjectId;
                        var url       = projectId != null ? $"https://console.firebase.google.com/project/{projectId}/settings/general" : "https://console.firebase.google.com/";
                        if (HelpBoxUtils.Draw($"{platform}: {message}", MessageType.Error, new GUIContent("Open Firebase", url))) Application.OpenURL(url);
                        break;
                    case FirebaseProjectFiles.ProblemFix.PlayerSettings:
                        if (HelpBoxUtils.Draw($"{platform}: {message}", MessageType.Error, new GUIContent("Player Settings", "Opens Project Settings > Player, whose Other Settings hold the app id"))) SettingsService.OpenProjectSettings("Project/Player");
                        break;
                    default:
                        HelpBoxUtils.Draw($"{platform}: {message}", MessageType.Error);
                        break;
                }
            }
        }

        // The file as an object field shows an asset, with the project it points to: a click pings it, a double click opens it, as Unity's
        // field does, and None with the file's name as Unity's has the type, both with the file's icon. No picker, as the page finds the file
        // wherever it is; a file with problems shows None, its problems at the page's top.
        private static void DrawConfigFile(string platform, FirebaseConfigFile file, string fileName) {
            var field   = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent(platform));
            var asset   = file.Exists && file.Paths.Count == 1 && file.Error == null ? AssetDatabase.LoadMainAssetAtPath(file.Path) : null;
            var content = asset ? new GUIContent($"{asset.name} ({file.ProjectId})", AssetPreview.GetMiniThumbnail(asset), file.Path) : new GUIContent($"None ({fileName})", AssetPreview.GetMiniTypeThumbnail(Path.GetExtension(fileName) == ".json" ? typeof(TextAsset) : typeof(DefaultAsset)));
            var id      = GUIUtility.GetControlID(FocusType.Passive, field);
            var e       = Event.current;
            if (e.type is EventType.Repaint) {
                EditorStyles.objectField.Draw(field, content, id, false, field.Contains(e.mousePosition));
            } else if (asset && e.type is EventType.MouseDown && e.button == 0 && field.Contains(e.mousePosition)) {
                if (e.clickCount == 2) AssetDatabase.OpenAsset(asset);
                else EditorGUIUtility.PingObject(asset);
                e.Use();
            }

            if (asset) GUI.Label(field, new GUIContent("", file.Path));
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
            // The label keeps its column; on a narrow page the button gives up a third of what is left to the account.
            var line    = EditorGUILayout.GetControlRect();
            var value   = EditorGUI.PrefixLabel(line, new GUIContent("Google account"));
            var refresh = new Rect(value.xMax - Section.ButtonWidth, line.y, Section.ButtonWidth, line.height);
            var room    = Mathf.Max(0, refresh.x - Section.LabelGap - value.x);
            var width   = room >= 110 + 60 ? 110 : room * 2 / 3;
            var button  = new Rect(refresh.x - Section.LabelGap - width, line.y, width, line.height);
            value.xMax  = Mathf.Max(value.x, button.x - Section.LabelGap);
            // A check under way spins beside what the last one found.
            EditorGUIUtility.SetIconSize(new Vector2(Section.IconSize, Section.IconSize));
            // One line, cut short where the button leaves less room; the whole text on hover.
            GUI.Label(value, checking ? SpinnerUtils.Content(text) : new GUIContent(text, text), Section.CutLabel);
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

#if FIREBASE_SUPPORT_APPCHECK
        private static void DrawEditorToken() {
            var token = EditorUserSettings.GetConfigValue(FirebaseInit.AppCheckEditorTokenKey) ?? "";
            EditorGUI.BeginChangeCheck();
            token = EditorGUILayout.DelayedTextField(new GUIContent("Editor Debug Token", "A debug token made in the Firebase console, App Check > Apps > Manage debug tokens. Kept for you only, in UserSettings."), token);
            if (EditorGUI.EndChangeCheck()) EditorUserSettings.SetConfigValue(FirebaseInit.AppCheckEditorTokenKey, token.Trim());
            if (token.Length == 0) HelpBoxUtils.Draw("Play mode sends no valid App Check token without one; enforced services then refuse the Editor.", MessageType.Info);
        }
#endif

#if !FIREBASE_SUPPORT_APPCHECK
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