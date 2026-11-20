using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Google.MiniJSON;

namespace RiseOn.Firebase.RemoteConfig.Editor {
    /// <summary>The console's rules for keys and values, and the mapping between its value types and the package's.</summary>
    internal static class RemoteParameterRules {
        public const int MaxParameters = 2000;

        // Firebase: a key starts with an underscore or an English letter and may also have digits; 256 characters at most.
        private static readonly Regex KeyPattern = new("^[A-Za-z_][A-Za-z0-9_]{0,255}$");

        public static bool IsValidKey(string key) => key != null && KeyPattern.IsMatch(key);

        public static string ConsoleType(RemoteValueType type) {
            switch (type) {
                case RemoteValueType.Bool:   return "BOOLEAN";
                case RemoteValueType.Int:    return "NUMBER";
                case RemoteValueType.Float:  return "NUMBER";
                case RemoteValueType.String: return "STRING";
                case RemoteValueType.Json:   return "JSON";
                default:                     throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>The package type for a console parameter; NUMBER becomes Int when every value it holds is whole.</summary>
        public static RemoteValueType LocalType(string consoleType, IEnumerable<string> values) {
            switch (consoleType) {
                case "BOOLEAN": return RemoteValueType.Bool;
                case "NUMBER":  return values.All(value => value == null || RemoteValue.ParseInt(value, out _)) ? RemoteValueType.Int : RemoteValueType.Float;
                case "JSON":    return RemoteValueType.Json;
                default:        return RemoteValueType.String;
            }
        }

        /// <summary>Why <paramref name="text"/> is not a value of <paramref name="type"/>; null when it is.</summary>
        public static string CheckValue(RemoteValueType type, string text) {
            switch (type) {
                case RemoteValueType.Bool:   return text is "true" or "false" ? null : "must be true or false";
                case RemoteValueType.Int:    return RemoteValue.ParseInt(text, out _) ? null : "must be a whole number";
                case RemoteValueType.Float:  return RemoteValue.ParseFloat(text, out _) ? null : "must be a number with a dot for decimals";
                case RemoteValueType.String: return null;
                case RemoteValueType.Json:   return Json.Deserialize(text) != null ? null : "must be valid JSON";
                default:                     throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static bool SameValue(RemoteValueType type, string a, string b) {
            a ??= "";
            b ??= "";
            switch (type) {
                case RemoteValueType.Bool:
                    return RemoteValue.ParseBool(a, out var boolA) && RemoteValue.ParseBool(b, out var boolB) ? boolA == boolB : a == b;
                case RemoteValueType.Int:
                case RemoteValueType.Float:
                    return double.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var numberA)
                        && double.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var numberB)
                         ? numberA.Equals(numberB)
                         : a == b;
                case RemoteValueType.Json:
                    var jsonA = Json.Deserialize(a);
                    var jsonB = Json.Deserialize(b);
                    return jsonA != null && jsonB != null ? Json.Serialize(jsonA) == Json.Serialize(jsonB) : a == b;
                case RemoteValueType.String:
                    return a == b;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>The empty value of a type, for a new parameter.</summary>
        public static string ZeroValue(RemoteValueType type) {
            switch (type) {
                case RemoteValueType.Bool:   return "false";
                case RemoteValueType.Int:    return "0";
                case RemoteValueType.Float:  return "0";
                case RemoteValueType.String: return "";
                case RemoteValueType.Json:   return "{}";
                default:                     throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>Everything that stops the parameters from working or from being pushed, one line each.</summary>
        public static List<string> Problems(FirebaseRemoteConfigSettings settings) {
            var problems   = new List<string>();
            var parameters = settings.Parameters;

            if (parameters.Count > MaxParameters) problems.Add($"Firebase allows {MaxParameters} parameters; there are {parameters.Count}.");

            foreach (var parameter in parameters) {
                // The names a valid key can have that C# refuses where the generator puts them, whatever the escape.
                if (parameter.Key == "value__") problems.Add("C# keeps value__ for itself in every enum, so RemoteKey cannot have it.");
                if (parameter.Key == nameof(RemoteKeys)) problems.Add("RemoteKeys cannot have a property of its own name, so no key can be RemoteKeys.");
                if (!IsValidKey(parameter.Key)) problems.Add($"\"{parameter.Key}\" is not a valid key: a letter or underscore, then letters, digits or underscores.");

                var valueProblem = CheckValue(parameter.Type, parameter.DefaultValue ?? "");
                if (valueProblem != null) problems.Add($"{parameter.Key}: the default {valueProblem}.");
            }

            problems.AddRange(parameters
                .GroupBy(parameter => parameter.Key)
                .Where(group => group.Count() > 1)
                .Select(group => $"The key {group.Key} is used {group.Count()} times."));
            // NewId never repeats an id of the list it adds to, so only two branches meet here, under one in a hundred
            // thousand for 200 parameters. Each id may be saved in enum fields on its branch, so only a person can
            // tell which parameter keeps it.
            problems.AddRange(parameters
                .Where(parameter => parameter.Id > 0)
                .GroupBy(parameter => parameter.Id)
                .Where(group => group.Count() > 1)
                .Select(group => $"Keys {string.Join(", ", group.Select(parameter => parameter.Key))} share the id {group.Key}. Remove the one serialized enum fields do not mean and add it again for a new id."));
            return problems;
        }
    }
}