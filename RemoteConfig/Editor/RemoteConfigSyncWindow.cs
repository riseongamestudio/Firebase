using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using RiseOn.Firebase.Editor;
using RiseOn.Utils.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Firebase.RemoteConfig.Editor {
    /// <summary>
    /// Shows what a pull or a push would change, key by key, and applies the ticked rows.<br/>
    /// A push fetches the latest template again and writes only the ticked keys into it, so edits made in the console since are kept.
    /// </summary>
    internal sealed class RemoteConfigSyncWindow : EditorWindow {
        [SerializeField] private RemoteSyncDirection direction;

        private List<RemoteDiffRow> rows;
        private string              version;
        private string              status;
        private MessageType         statusType;
        private bool                busy;
        private bool                showSame;
        private Vector2             scroll;

        public static void Open(RemoteSyncDirection direction) {
            var window = GetWindow<RemoteConfigSyncWindow>(true);
            window.direction    = direction;
            window.titleContent = new GUIContent($"Remote Config {direction}");
            window.minSize      = new Vector2(640, 360);
            window.RefreshAsync().Forget();
        }

        private void OnEnable() {
            if (rows == null && !busy) EditorApplication.delayCall += () => RefreshAsync().Forget();
        }

        private void OnGUI() {
            var projectId = FirebaseProjectFiles.ProjectId;
            var account   = FirebaseAccess.Describe(out var problem);
            EditorGUILayout.LabelField(direction is RemoteSyncDirection.Pull ? "Pull from" : "Push to", $"{projectId}{(version != null ? $", version {version}" : "")}");
            if (FirebaseAccess.IsChecking) SpinnerUtils.LabelField(new GUIContent("As"), account ?? problem);
            else EditorGUILayout.LabelField("As", account ?? problem);

            // Work under way spins; its outcome stays as a message.
            if (busy && status != null) SpinnerUtils.LabelField(GUIContent.none, status);
            else if (status != null) HelpBoxUtils.Draw(status, statusType);

            using (new EditorGUI.DisabledScope(busy)) {
                using (new EditorGUILayout.HorizontalScope()) {
                    showSame = EditorGUILayout.ToggleLeft("Show unchanged", showSame, GUILayout.Width(130));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Refresh", GUILayout.Width(80))) RefreshAsync().Forget();
                }

                DrawRows();

                using (new EditorGUILayout.HorizontalScope()) {
                    GUILayout.FlexibleSpace();
                    var selected = Selected().Count();
                    using (new EditorGUI.DisabledScope(rows == null || selected == 0)) {
                        if (direction is RemoteSyncDirection.Pull) {
                            if (GUILayout.Button($"Pull {selected} change{(selected == 1 ? "" : "s")}", GUILayout.Width(140))) Pull();
                        } else {
                            if (FirebaseAccess.Mode is FirebaseAccessMode.ServiceAccount && GUILayout.Button("Validate", GUILayout.Width(80))) PushAsync(publish: false).Forget();
                            if (GUILayout.Button($"Publish {selected} change{(selected == 1 ? "" : "s")}", GUILayout.Width(140))) PushAsync(publish: true).Forget();
                        }
                    }
                }
            }
        }

        private void DrawRows() {
            if (rows == null) {
                GUILayout.FlexibleSpace();
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                GUILayout.Space(20);
                GUILayout.Label("Key", GUILayout.Width(200));
                GUILayout.Label("Change", GUILayout.Width(70));
                GUILayout.Label("Project", GUILayout.Width(150));
                GUILayout.Label("Console", GUILayout.Width(150));
                GUILayout.Label("Differs in");
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var row in rows.Where(row => showSame || row.Change is not RemoteChange.Same)) {
                using (new EditorGUILayout.HorizontalScope()) {
                    using (new EditorGUI.DisabledScope(row.Change is RemoteChange.Same)) {
                        row.Selected = EditorGUILayout.Toggle(row.Selected && row.Change is not RemoteChange.Same, GUILayout.Width(16));
                    }

                    GUILayout.Space(4);
                    EditorGUILayout.SelectableLabel(row.Key, GUILayout.Width(200), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    GUILayout.Label(row.Change.ToString(), GUILayout.Width(70));
                    GUILayout.Label(new GUIContent(Shorten(row.Local?.DefaultValue), row.Local?.DefaultValue), GUILayout.Width(150));
                    GUILayout.Label(new GUIContent(ConsoleText(row.Console), ConsoleTooltip(row.Console)), GUILayout.Width(150));
                    GUILayout.Label(row.Detail);
                }
            }

            EditorGUILayout.EndScrollView();
            if (!rows.Any(row => showSame || row.Change is not RemoteChange.Same)) HelpBoxUtils.Draw("The project and the console have the same parameters.", MessageType.Info);
        }

        private IEnumerable<RemoteDiffRow> Selected() {
            return rows?.Where(row => row.Selected && row.Change is not RemoteChange.Same) ?? Enumerable.Empty<RemoteDiffRow>();
        }

        private async UniTask RefreshAsync() {
            var projectId = FirebaseProjectFiles.ProjectId;
            if (projectId == null) {
                SetStatus("The project has no Firebase config file, or its files name different projects.", MessageType.Error);
                return;
            }

            busy = true;
            SetStatus("Fetching the template", MessageType.None);
            try {
                var template = await RemoteConfigConsole.FetchAsync(projectId);
                version = template.VersionNumber;
                rows    = RemoteConfigDiff.Compare(RemoteConfigSettingsProvider.LoadOrCreate(), template, direction);
                SetStatus(null, MessageType.None);
            } catch (Exception e) {
                SetStatus(e.Message, MessageType.Error);
            } finally {
                busy = false;
                Repaint();
            }
        }

        private void Pull() {
            var settings = RemoteConfigSettingsProvider.LoadOrCreate();
            var selected = Selected().ToArray();
            Undo.RecordObject(settings, "Pull Remote Config");
            RemoteConfigDiff.ApplyPull(settings, selected);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            RemoteKeysFile.Sync(settings);

            Debug.Log($"{nameof(RemoteConfigSyncWindow)}: pulled {string.Join(", ", selected.Select(row => $"{row.Change} {row.Key}"))}");
            RefreshAsync().Forget();
        }

        private async UniTaskVoid PushAsync(bool publish) {
            var projectId = FirebaseProjectFiles.ProjectId;
            var settings  = RemoteConfigSettingsProvider.LoadOrCreate();
            var problems  = RemoteParameterRules.Problems(settings);
            if (problems.Count > 0) {
                SetStatus(string.Join("\n", problems), MessageType.Error);
                return;
            }

            var selected = Selected().ToArray();
            busy = true;
            try {
                SetStatus("Fetching the latest template", MessageType.None);
                var template = await RemoteConfigConsole.FetchAsync(projectId);
                RemoteConfigDiff.ApplyPush(settings, template, selected);

                // Only the REST API checks without publishing; through the CLI the server checks at publish.
                if (FirebaseAccess.Mode is FirebaseAccessMode.ServiceAccount) {
                    SetStatus("Validating", MessageType.None);
                    await RemoteConfigConsole.ValidateAsync(projectId, template);
                }
                if (!publish) {
                    SetStatus("Firebase accepts the template.", MessageType.Info);
                    return;
                }

                var summary = string.Join("\n", selected.Take(20).Select(row => $"{row.Change} {row.Key}")) + (selected.Length > 20 ? $"\nand {selected.Length - 20} more" : "");
                if (!EditorUtility.DisplayDialog("Publish Remote Config", $"Publish to {projectId}? Players get it at their next fetch.\n\n{summary}", "Publish", "Cancel")) {
                    SetStatus(null, MessageType.None);
                    return;
                }

                SetStatus("Publishing", MessageType.None);
                await RemoteConfigConsole.PublishAsync(projectId, template);
                Debug.Log($"{nameof(RemoteConfigSyncWindow)}: published {string.Join(", ", selected.Select(row => $"{row.Change} {row.Key}"))} to {projectId}");
                busy = false;
                await RefreshAsync();
                SetStatus($"Published version {version}.", MessageType.Info);
            } catch (Exception e) {
                SetStatus(e.Message, MessageType.Error);
            } finally {
                busy = false;
                Repaint();
            }
        }

        private void SetStatus(string text, MessageType type) {
            status     = text;
            statusType = type;
            Repaint();
        }

        private static string ConsoleText(ConsoleParameter console) {
            if (console == null) return "";
            if (console.DefaultValue == null) return "(in-app default)";

            var conditions = console.ConditionalValues.Count;
            return Shorten(console.DefaultValue) + (conditions > 0 ? $" +{conditions}" : "");
        }

        private static string ConsoleTooltip(ConsoleParameter console) {
            if (console == null) return null;

            return $"{console.ValueType}: {console.DefaultValue ?? "in-app default"}{(console.ConditionalValues.Count > 0 ? $"\n{console.ConditionalValues.Count} conditional values, kept as they are" : "")}";
        }

        private static string Shorten(string text) {
            if (text == null) return "";

            text = text.Replace("\n", " ");
            return text.Length > 24 ? text.Substring(0, 23) + "…" : text;
        }
    }
}