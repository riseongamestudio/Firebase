using System.Linq;
using RiseOn.Firebase.Editor;
using RiseOn.Utils.Editor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Firebase.RemoteConfig.Editor {
    /// <summary>
    /// Project Settings > RiseOn > Firebase > Remote Config: the parameters, which code reads through RemoteKeys, the fetch settings, and pull and push with the Firebase console.<br/>
    /// Edits go straight to the asset, with undo; the keys file waits for the page to close, since writing it recompiles.
    /// </summary>
    internal static class RemoteConfigSettingsProvider {
        internal const string PagePath = FirebaseSettingsProvider.PagePath + "/Remote Config";

        private static readonly SettingsTree tree = new();
        private static readonly SectionRows  rows = new();

        // A recompile closes the page as the domain unloads: a write then would land after the compile and wait,
        // unseen, for the next one. The page comes back after the reload and still says the file waits.
        private static bool reloading;

        [InitializeOnLoadMethod]
        private static void WatchReload() => AssemblyReloadEvents.beforeAssemblyReload += () => reloading = true;

        [MenuItem("RiseOn/Firebase/Remote Config", priority = 1)]
        internal static void Open() {
            SettingsService.OpenProjectSettings(PagePath);
        }

        [SettingsProvider]
        private static SettingsProvider Create() {
            // Unity deactivates the page it rebuilds after a reload before it draws; only a drawn page has edits to write.
            var drawn = false;

            return new SettingsProvider(PagePath, SettingsScope.Project) {
                guiHandler = _ => {
                    using var page = SettingsPage.Begin();
                    RemoteKeysFile.Held = true;
                    drawn = true;
                    Draw(LoadOrCreate());
                }
              , deactivateHandler = () => {
                    // Leaving or closing the window asks once, for every edit made here: one write and one recompile, or none.
                    RemoteKeysFile.Held = false;
                    var settings = Find();
                    if (settings && drawn && !reloading && RemoteKeysFile.IsPending(settings)) AskOnLeave(settings);
                    drawn = false;
                    tree.Dispose();
                }
              , keywords = new[] { "remote config", "firebase", "parameter", "fetch", "live", "realtime" }
            };
        }

        /// <summary>The whole page for one settings asset.</summary>
        internal static void Draw(FirebaseRemoteConfigSettings settings) {
            // The same problems stop the build.
            if (OutsideResources() is { } outside && HelpBoxUtils.Draw(outside, MessageType.Error, new GUIContent("Move it"))) {
                SettingsAssetUtils.MoveToResources<FirebaseRemoteConfigSettings>(FirebaseRemoteConfigSettings.ResourceName);
            }

            foreach (var problem in RemoteParameterRules.Problems(settings)) HelpBoxUtils.Draw(problem, MessageType.Error);

            // The page's edits until they are applied or reverted; otherwise Unity stopping before the write, or a file edited by hand.
            if (RemoteKeysFile.IsCurrent(settings)) {
                RemoteKeysFile.Remember(settings);
            } else if (RemoteKeysFile.IsPending(settings)) {
                var revert  = RemoteKeysFile.RevertProblem();
                var renames = RemoteKeysFile.Renames(settings);
                var renamed = renames.Count == 0 ? "" : $" Apply also renames {string.Join(", ", renames.Select(rename => $"{rename.From} to {rename.To}"))} where the project's scripts name it through RemoteKey or RemoteKeys.";
                var picked  = HelpBoxUtils.Draw($"{RemoteKeysFile.FilePath} does not list the keys yet, so RemoteKeys is out of date and builds stop. Apply writes it; Revert takes the parameters back to the last time they matched it.{renamed}", MessageType.Warning,
                    // Revert then Apply, as Unity's import settings order them.
                    (new GUIContent("Revert", revert ?? "Takes the parameters back to the last time they matched the keys file, defaults and Live included"), revert == null), (new GUIContent("Apply", "Writes the keys file; Unity recompiles RemoteKeys"), true));
                if (picked == 1) RemoteKeysFile.Sync(settings);
                if (picked == 0) {
                    RemoteKeysFile.Revert(settings);
                    GUIUtility.ExitGUI();
                }
            }

            // The project and the access to it are set on the Firebase page; this page only says when Pull and Push cannot use them.
            var projectId = FirebaseProjectFiles.ProjectId;
            var account   = FirebaseAccess.Describe(out var accessProblem);
            var blocked   = projectId == null ? NoProjectReason : account == null ? accessProblem : null;
            if (blocked != null && !FirebaseAccess.IsChecking
             && HelpBoxUtils.Draw($"Pull and Push cannot reach the Firebase project. {blocked}", MessageType.Warning, new GUIContent("Open Firebase"))) {
                FirebaseSettingsProvider.Open();
                GUIUtility.ExitGUI();
            }

            var root       = tree.Begin(settings);
            var parameters = root.Children["parameters"];
            var resolver   = (IOrderedCollectionResolver) parameters.ChildResolver;

            // The short sections first, the parameters list, which grows long, last.
            foreach (var child in root.Children) {
                if (child != parameters) child.Draw();
            }

            using (var section = Section.Foldout("RiseOn.RemoteConfig.Parameters", "Parameters", "The keys the game reads, with the values it uses until a fetch brings the console's.", buttons: 4, padded: false)) {
                using (new EditorGUI.DisabledScope(projectId == null)) {
                    if (section.Button(3, SdfIconType.BoxArrowUpRight, projectId == null ? $"Console is off.\n{NoProjectReason}" : "Open the project's Remote Config in the Firebase console")) {
                        Application.OpenURL(RemoteConfigConsole.ConsoleUrl(projectId));
                    }
                }

                // Off without a project or access; the tooltip then says which, so the button does not just sit gray.
                using (new EditorGUI.DisabledScope(blocked != null)) {
                    if (section.Button(2, SdfIconType.CloudDownload, blocked == null ? "Pull: take the console's parameters, choosing which" : $"Pull is off.\n{blocked}")) RemoteConfigSyncWindow.Open(RemoteSyncDirection.Pull);
                    if (section.Button(1, SdfIconType.CloudUpload, blocked == null ? "Push: publish these parameters to the console, choosing which" : $"Push is off.\n{blocked}")) RemoteConfigSyncWindow.Open(RemoteSyncDirection.Push);
                }

                if (section.Button(0, SdfIconType.Plus, "Add a parameter")) {
                    resolver.QueueAdd(new object[] { NewParameter(settings) });
                    // The new row shows, on a folded section too.
                    SessionState.SetBool("RiseOn.RemoteConfig.Parameters", true);
                }

                if (section.Open) {
                    rows.Draw(parameters.Children.Count, i => parameters.Children[i].Draw(null), "None", (from, to) => {
                        // Odin's resolver has no move, so the parameter leaves its place and comes back at the other.
                        var moved = parameters.Children[from].ValueEntry.WeakSmartValue;
                        resolver.QueueRemoveAt(from);
                        resolver.QueueInsertAt(to, new[] { moved });
                    }, resolver.QueueRemoveAt, "Remove the parameter");
                }
            }

            tree.End();
        }

        // Discard needs what the file last matched; without it the choice is to apply now or leave the file waiting, with builds stopped.
        private static void AskOnLeave(FirebaseRemoteConfigSettings settings) {
            const string message = "The parameters changed since RemoteKeys was last written.";
            var revert = RemoteKeysFile.RevertProblem();
            if (revert == null) {
                if (EditorUtility.DisplayDialog("Remote Config", message, "Apply", "Discard")) RemoteKeysFile.Sync(settings);
                else RemoteKeysFile.Revert(settings);
            } else if (EditorUtility.DisplayDialog("Remote Config", $"{message} They cannot be discarded: {revert.Substring(revert.IndexOf('\n') + 1)}", "Apply", "Later")) {
                RemoteKeysFile.Sync(settings);
            }
        }

        /// <summary>The project's settings asset wherever the project keeps it, made empty the first time the page opens.</summary>
        internal static FirebaseRemoteConfigSettings LoadOrCreate() => SettingsAssetUtils.LoadOrCreate<FirebaseRemoteConfigSettings>(FirebaseRemoteConfigSettings.ResourceName);

        /// <summary>The project's settings asset wherever the project keeps it; null when it has none.</summary>
        internal static FirebaseRemoteConfigSettings Find() => SettingsAssetUtils.Find<FirebaseRemoteConfigSettings>(FirebaseRemoteConfigSettings.ResourceName);

        /// <summary>Why the game cannot load the asset, which it reads from Resources: null when it can, or when there is none and the game runs without parameters.</summary>
        internal static string OutsideResources() =>
            SettingsAssetUtils.OutsideResources<FirebaseRemoteConfigSettings>(FirebaseRemoteConfigSettings.ResourceName) is { } path ? $"{path} is outside a Resources folder, so the game cannot load it and runs without parameters." : null;

        private const string NoProjectReason = "No Firebase project yet: add google-services.json or GoogleService-Info.plist.";

        // A parameter numbered and keyed apart from the others, a string with no default.
        private static RemoteParameter NewParameter(FirebaseRemoteConfigSettings settings) =>
            new() { Id = RemoteKeysFile.NewId(settings), Key = UnusedKey(settings), Type = RemoteValueType.String, DefaultValue = "" };

        private static string UnusedKey(FirebaseRemoteConfigSettings settings) {
            for (var i = 1;; i++) {
                var key = $"new_parameter_{i}";
                if (settings.Parameters.All(parameter => parameter.Key != key)) return key;
            }
        }
    }

    [CustomEditor(typeof(FirebaseRemoteConfigSettings))]
    internal sealed class RemoteConfigSettingsInspector : SettingsAssetInspector {
        protected override string PagePath => RemoteConfigSettingsProvider.PagePath;
    }
}