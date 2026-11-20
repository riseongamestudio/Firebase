using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RiseOn.Utils.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RiseOn.Firebase.RemoteConfig.Editor {
    /// <summary>
    /// The keys as the pack's source generator reads them, one "id, key, type, description" line each, split by tabs; it compiles them into RiseOn.Firebase.RemoteConfig as <see cref="RemoteKey"/> and the properties of <see cref="RemoteKeys"/>.<br/>
    /// Defaults and the live flag stay out, so editing them does not recompile.<br/>
    /// Written when Apply is pressed or chosen as the settings page closes, after a pull from the console, and when the asset is imported from elsewhere (a pull, a merge) while the page is closed; never by hand.<br/>
    /// A write renames the keys the project's scripts name through RemoteKey and RemoteKeys too, so they keep compiling.
    /// </summary>
    internal sealed class RemoteKeysFile : AssetPostprocessor {
        // Unity hands a project's *.{generator assembly}.additionalfile to that generator.
        internal const string FilePath = "Assets/RiseOn/RemoteConfig/Keys.RiseOnRemoteConfigGenerator.additionalfile";

        /// <summary>While the settings page draws: it saves the asset on every edit, and writes the file only when Apply is pressed or chosen as it closes.</summary>
        internal static bool held;

        // The parameters as they were the last time the page saw them match the file, for Revert. In Library, as only this machine kept them.
        private const string AppliedPath = "Library/RiseOn/RemoteConfigApplied.json";

        private static string  appliedJson;
        private static Applied applied;

        [Serializable]
        private sealed class Applied {
            public string                keys;
            public List<RemoteParameter> parameters;
        }

        /// <summary>The file says what the parameters say.</summary>
        internal static bool IsCurrent(FirebaseRemoteConfigSettings settings) => FileText() == Text(settings);

        /// <summary>The asset's keys are not the file's, and nothing stops the file from being written.</summary>
        internal static bool IsPending(FirebaseRemoteConfigSettings settings) => RemoteParameterRules.Problems(settings).Count == 0 && !IsCurrent(settings);

        /// <summary>The keys whose name changed since the file was written, by id, as code names them.</summary>
        internal static List<(string From, string To)> Renames(FirebaseRemoteConfigSettings settings) {
            var written = new Dictionary<int, string>();
            foreach (var line in FileText().Split('\n')) {
                var parts = line.Split('\t');
                if (parts.Length == 4 && int.TryParse(parts[0], out var id)) written[id] = parts[1];
            }

            return settings.Parameters.Where(parameter => written.TryGetValue(parameter.Id, out var key) && key != parameter.Key).Select(parameter => (written[parameter.Id], parameter.Key)).ToList();
        }

        /// <summary>
        /// Writes the file when its text would change and the parameters have no problem, renaming the keys the project's scripts name first; Unity recompiles after.
        /// </summary>
        internal static void Sync(FirebaseRemoteConfigSettings settings) {
            Number(settings);
            if (!IsPending(settings)) return;

            var renames = Renames(settings);
            var renamed = CodeRename.Members(nameof(RemoteKey), renames).Union(CodeRename.Members(nameof(RemoteKeys), renames)).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, Text(settings), new UTF8Encoding(false));
            Remember(settings);
            // One refresh, so the renamed scripts and the new keys compile together.
            AssetDatabase.Refresh();
            if (renamed.Count > 0) Debug.Log($"Remote Config: renamed keys in {string.Join(", ", renamed)}");
        }

        /// <summary>Keeps the parameters while the file matches them, so <see cref="Revert"/> can come back to them; the page calls it as it draws.</summary>
        internal static void Remember(FirebaseRemoteConfigSettings settings) {
            var json = JsonUtility.ToJson(new Applied { keys = Text(settings), parameters = settings.Parameters });
            if (json == LoadAppliedJson()) return;

            Directory.CreateDirectory(Path.GetDirectoryName(AppliedPath)!);
            File.WriteAllText(AppliedPath, json, new UTF8Encoding(false));
            appliedJson = json;
            applied     = null;
        }

        /// <summary>Why <see cref="Revert"/> cannot run, as a button's tooltip; null when it can.</summary>
        internal static string RevertProblem() {
            var last = LoadApplied();
            if (last == null) return "Revert is off.\nThis machine kept no parameters from the last time they matched the keys file.";
            if (last.keys != FileText()) return "Revert is off.\nThe keys file changed elsewhere (a pull, a merge) after this machine last wrote it.";
            return null;
        }

        /// <summary>Takes the parameters back to when the file last matched them, with undo; check <see cref="RevertProblem"/> first.</summary>
        internal static void Revert(FirebaseRemoteConfigSettings settings) {
            var last = LoadApplied();
            Undo.RecordObject(settings, "Revert Remote Config Parameters");
            settings.Parameters.Clear();
            settings.Parameters.AddRange(last.parameters);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        private static string FileText() {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath) : "";
        }

        private static string LoadAppliedJson() {
            appliedJson ??= File.Exists(AppliedPath) ? File.ReadAllText(AppliedPath) : "";
            return appliedJson;
        }

        private static Applied LoadApplied() {
            var json = LoadAppliedJson();
            if (json.Length == 0) return null;

            try {
                applied ??= JsonUtility.FromJson<Applied>(json);
            } catch (ArgumentException) {
                // A file cut short reads as none.
                return null;
            }

            return applied;
        }

        /// <summary>
        /// A random positive id no parameter of <paramref name="settings"/> has.<br/>
        /// Random, as Localization's DistributedUIDGenerator keeps key ids apart across machines, so parameters that two branches add do not meet on one id; 31 bits, as Unity serializes enums of 32 bits or smaller.
        /// </summary>
        internal static int NewId(FirebaseRemoteConfigSettings settings) {
            while (true) {
                var id = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0) & int.MaxValue;
                if (id > 0 && settings.Parameters.All(parameter => parameter.Id != id)) return id;
            }
        }

        /// <summary>Gives an id to the parameters a hand edit left without one; the page gives the ones its + button adds.</summary>
        private static void Number(FirebaseRemoteConfigSettings settings) {
            var asset      = new SerializedObject(settings);
            var parameters = asset.FindProperty("parameters");
            for (var i = 0; i < parameters.arraySize; ++i) {
                var id = parameters.GetArrayElementAtIndex(i).FindPropertyRelative("id");
                if (id.intValue > 0) continue;

                id.intValue = NewId(settings);
                asset.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssetIfDirty(settings);
        }

        private static string Text(FirebaseRemoteConfigSettings settings) {
            var text = new StringBuilder();
            foreach (var parameter in settings.Parameters.OrderBy(parameter => parameter.Id)) {
                var description = new string((parameter.Description ?? "").Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
                text.Append($"{parameter.Id}\t{parameter.Key}\t{TypeName(parameter.Type)}\t{description}\n");
            }

            return text.ToString();
        }

        private static string TypeName(RemoteValueType type) {
            switch (type) {
                case RemoteValueType.Bool:   return "bool";
                case RemoteValueType.Int:    return "int";
                case RemoteValueType.Float:  return "float";
                case RemoteValueType.String: return "string";
                case RemoteValueType.Json:   return "string";
                default:                     throw new System.ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        // A pull or a merge changed the settings, or someone deleted the file.
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom) {
            if (held) return;
            if (!imported.Any(path => Path.GetFileNameWithoutExtension(path) is FirebaseRemoteConfigSettings.ResourceName) && !deleted.Contains(FilePath)) return;

            var settings = RemoteConfigSettingsProvider.Find();
            if (settings) Sync(settings);
        }
    }

    /// <summary>Stops a build whose RemoteKeys would not match the parameters it reads.</summary>
    internal sealed class RemoteKeysBuildCheck : IPreprocessBuildWithReport {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) {
            if (RemoteConfigSettingsProvider.OutsideResources() is { } outside) throw new BuildFailedException($"Remote Config: {outside} Move it in Project Settings > RiseOn > Firebase > Remote Config.");

            var settings = RemoteConfigSettingsProvider.Find();
            if (!settings) return;

            var problems = RemoteParameterRules.Problems(settings);
            if (problems.Count > 0) throw new BuildFailedException("Remote Config: fix the parameters before building (Project Settings > RiseOn > Firebase > Remote Config):\n- " + string.Join("\n- ", problems));
            if (!RemoteKeysFile.IsCurrent(settings)) throw new BuildFailedException($"Remote Config: {RemoteKeysFile.FilePath} does not list the parameters, so RemoteKeys is out of date; open Project Settings > RiseOn > Firebase > Remote Config and press Apply.");
        }
    }
}