using System.Collections.Generic;
using System.Linq;
using Google.MiniJSON;

namespace RiseOn.Firebase.RemoteConfig.Editor {
    /// <summary>A parameter as the console has it, read from the template.</summary>
    internal sealed class ConsoleParameter {
        public string Key { get; }
        public string Group { get; }
        public string Description { get; }

        /// <summary>BOOLEAN, NUMBER, STRING or JSON; STRING for parameters made before the console had types.</summary>
        public string ValueType { get; }

        /// <summary>Null when the console serves the in-app default.</summary>
        public string DefaultValue { get; }

        public IReadOnlyList<string> ConditionalValues { get; }

        public RemoteValueType LocalType => RemoteParameterRules.LocalType(ValueType, ConditionalValues.Prepend(DefaultValue));

        public ConsoleParameter(string key, string group, Dictionary<string, object> json) {
            Key               = key;
            Group             = group ?? "";
            Description       = json.TryGetValue("description", out var description) ? (string)description : "";
            ValueType         = json.TryGetValue("valueType", out var type) ? (string)type : "STRING";
            DefaultValue      = Value(json.TryGetValue("defaultValue", out var value) ? value : null);
            ConditionalValues = json.TryGetValue("conditionalValues", out var conditional) && conditional is Dictionary<string, object> conditions
                ? conditions.Values.Select(Value).ToArray()
                : new string[0];
        }

        private static string Value(object json) => json is Dictionary<string, object> value && value.TryGetValue("value", out var text) ? (string)text : null;
    }

    /// <summary>
    /// The project's whole Remote Config template, kept as the JSON the API gives so that publishing keeps everything the package does not touch: conditions, conditional values, groups, other parameters.
    /// </summary>
    internal sealed class RemoteTemplate {
        private const string ParametersKey = "parameters";
        private const string GroupsKey     = "parameterGroups";

        private readonly Dictionary<string, object> json;

        /// <summary>The REST API's version tag; null when the CLI fetched the template.</summary>
        public string ETag { get; }

        public string VersionNumber => json.TryGetValue("version", out var version) && version is Dictionary<string, object> info && info.TryGetValue("versionNumber", out var number) ? number as string : null;

        public RemoteTemplate(Dictionary<string, object> json, string eTag) {
            this.json = json;
            ETag      = eTag;
        }

        public Dictionary<string, ConsoleParameter> Parameters {
            get {
                var parameters = new Dictionary<string, ConsoleParameter>();
                foreach (var (key, value) in Entries(TopLevel(false))) parameters[key] = new ConsoleParameter(key, "", value);

                foreach (var (group, groupJson) in Groups()) {
                    foreach (var (key, value) in Entries(GroupParameters(groupJson, false))) parameters[key] = new ConsoleParameter(key, group, value);
                }

                return parameters;
            }
        }

        /// <summary>
        /// Writes the local parameter's type, default, description and group, keeping its conditional values; a console that serves the in-app default keeps doing so.
        /// </summary>
        public void Set(RemoteParameter parameter) {
            var existing = Detach(parameter.Key) ?? new Dictionary<string, object>();
            existing["valueType"] = RemoteParameterRules.ConsoleType(parameter.Type);

            if (!(existing.TryGetValue("defaultValue", out var current) && current is Dictionary<string, object> value && value.ContainsKey("useInAppDefault"))) {
                existing["defaultValue"] = new Dictionary<string, object> { ["value"] = parameter.DefaultValue ?? "" };
            }

            if (string.IsNullOrEmpty(parameter.Description)) existing.Remove("description");
            else existing["description"] = parameter.Description;

            var target = string.IsNullOrEmpty(parameter.Group) ? TopLevel(true) : GroupParameters(Group(parameter.Group), true);
            target[parameter.Key] = existing;
        }

        public void Remove(string key) {
            Detach(key);
        }

        /// <summary>The body to publish: the template without its output-only version.</summary>
        public string ToPublishJson(string versionDescription) {
            var body = new Dictionary<string, object>(json);
            body.Remove("etag");
            body["version"] = new Dictionary<string, object> { ["description"] = versionDescription };
            return Json.Serialize(body);
        }

        public string ToFileJson() {
            var body = new Dictionary<string, object>(json);
            body.Remove("etag");
            body.Remove("version");
            return Json.Serialize(body);
        }

        // Takes the parameter out of wherever it is, dropping a group it leaves empty.
        private Dictionary<string, object> Detach(string key) {
            var topLevel = TopLevel(false);
            if (topLevel != null && topLevel.TryGetValue(key, out var found)) {
                topLevel.Remove(key);
                return found as Dictionary<string, object>;
            }

            foreach (var (group, groupJson) in Groups().ToArray()) {
                var parameters = GroupParameters(groupJson, false);
                if (parameters == null || !parameters.TryGetValue(key, out found)) continue;

                parameters.Remove(key);
                if (parameters.Count == 0) ((Dictionary<string, object>)json[GroupsKey]).Remove(group);
                return found as Dictionary<string, object>;
            }

            return null;
        }

        private Dictionary<string, object> TopLevel(bool create) => Child(json, ParametersKey, create);

        private IEnumerable<(string name, Dictionary<string, object> json)> Groups() {
            var groups = Child(json, GroupsKey, false);
            return groups == null ? Enumerable.Empty<(string, Dictionary<string, object>)>() : Entries(groups);
        }

        private Dictionary<string, object> Group(string name) => Child(Child(json, GroupsKey, true), name, true);

        private static Dictionary<string, object> GroupParameters(Dictionary<string, object> group, bool create) => Child(group, ParametersKey, create);

        private static Dictionary<string, object> Child(Dictionary<string, object> parent, string key, bool create) {
            if (parent.TryGetValue(key, out var child) && child is Dictionary<string, object> dictionary) return dictionary;
            if (!create) return null;

            dictionary  = new Dictionary<string, object>();
            parent[key] = dictionary;
            return dictionary;
        }

        private static IEnumerable<(string key, Dictionary<string, object> value)> Entries(Dictionary<string, object> dictionary) {
            return dictionary == null
                ? Enumerable.Empty<(string, Dictionary<string, object>)>()
                : dictionary.Where(pair => pair.Value is Dictionary<string, object>).Select(pair => (pair.Key, (Dictionary<string, object>)pair.Value));
        }
    }
}