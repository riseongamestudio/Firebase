using System.Collections.Generic;
using System.Linq;

namespace RiseOn.Firebase.RemoteConfig.Editor {
    internal enum RemoteSyncDirection {
        Pull
      , Push
    }

    internal enum RemoteChange {
        Same
      , Add
      , Update
      , Remove
    }

    /// <summary>One key on either side, and what a pull or a push would do to it.</summary>
    internal sealed class RemoteDiffRow {
        public string           Key;
        public RemoteParameter  Local;
        public ConsoleParameter Console;
        public RemoteChange     Change;
        public string           Detail;
        public bool             Selected;
    }

    /// <summary>
    /// Compares the asset's parameters with the console's.<br/>
    /// Only what both sides have is compared: the type, the default, the description and the group.<br/>
    /// Conditional values stay the console's and the live flag the asset's.
    /// </summary>
    internal static class RemoteConfigDiff {
        public static List<RemoteDiffRow> Compare(FirebaseRemoteConfigSettings settings, RemoteTemplate template, RemoteSyncDirection direction) {
            var local   = settings.Parameters.Where(parameter => !string.IsNullOrEmpty(parameter.Key)).GroupBy(parameter => parameter.Key).ToDictionary(group => group.Key, group => group.First());
            var console = template.Parameters;
            var rows    = new List<RemoteDiffRow>();

            foreach (var key in local.Keys.Union(console.Keys).OrderBy(key => key)) {
                local.TryGetValue(key, out var mine);
                console.TryGetValue(key, out var theirs);

                var row = new RemoteDiffRow { Key = key, Local = mine, Console = theirs };
                if (mine == null) {
                    row.Change = direction is RemoteSyncDirection.Pull ? RemoteChange.Add : RemoteChange.Remove;
                    row.Detail = "only in the console";
                } else if (theirs == null) {
                    row.Change = direction is RemoteSyncDirection.Push ? RemoteChange.Add : RemoteChange.Remove;
                    row.Detail = "only in the project";
                } else {
                    row.Detail = string.Join(", ", Differences(mine, theirs));
                    row.Change = row.Detail.Length > 0 ? RemoteChange.Update : RemoteChange.Same;
                }

                // Removing is never the default: the other side may have a reason to keep a key this one lacks.
                row.Selected = row.Change is RemoteChange.Add or RemoteChange.Update;
                rows.Add(row);
            }

            return rows;
        }

        public static IEnumerable<string> Differences(RemoteParameter local, ConsoleParameter console) {
            if (RemoteParameterRules.ConsoleType(local.Type) != console.ValueType) yield return $"type {console.ValueType}";
            if (console.DefaultValue != null && !RemoteParameterRules.SameValue(local.Type, local.DefaultValue, console.DefaultValue)) yield return "default";
            if ((local.Description ?? "") != console.Description) yield return "description";
            if ((local.Group ?? "") != console.Group) yield return "group";
        }

        /// <summary>Writes the console's side of the selected rows into the asset.</summary>
        public static void ApplyPull(FirebaseRemoteConfigSettings settings, IEnumerable<RemoteDiffRow> rows) {
            foreach (var row in rows) {
                switch (row.Change) {
                    case RemoteChange.Add: {
                        var type = row.Console.LocalType;
                        var parameter = new RemoteParameter {
                            Id           = RemoteKeysFile.NewId(settings)
                          , Key          = row.Key
                          , Type         = type
                          , DefaultValue = row.Console.DefaultValue ?? RemoteParameterRules.ZeroValue(type)
                          , Description  = row.Console.Description
                          , Group        = row.Console.Group
                        };
                        settings.Parameters.Add(parameter);
                        break;
                    }
                    case RemoteChange.Update: {
                        var parameter = row.Local;
                        if (RemoteParameterRules.ConsoleType(parameter.Type) != row.Console.ValueType) parameter.Type = row.Console.LocalType;
                        if (row.Console.DefaultValue != null) parameter.DefaultValue = row.Console.DefaultValue;
                        parameter.Description = row.Console.Description;
                        parameter.Group       = row.Console.Group;
                        break;
                    }
                    case RemoteChange.Remove:
                        settings.Parameters.Remove(row.Local);
                        break;
                }
            }
        }

        /// <summary>Writes the asset's side of the selected rows into the template, by key.</summary>
        public static void ApplyPush(FirebaseRemoteConfigSettings settings, RemoteTemplate template, IEnumerable<RemoteDiffRow> rows) {
            foreach (var row in rows) {
                switch (row.Change) {
                    case RemoteChange.Add:
                    case RemoteChange.Update:
                        template.Set(settings.Parameters.First(parameter => parameter.Key == row.Key));
                        break;
                    case RemoteChange.Remove:
                        template.Remove(row.Key);
                        break;
                }
            }
        }
    }
}