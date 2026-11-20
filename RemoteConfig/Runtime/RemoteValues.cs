using System;
using System.Collections.Generic;
using System.Linq;

namespace RiseOn.Firebase.RemoteConfig {
    /// <summary>
    /// Every parameter of <see cref="FirebaseRemoteConfigSettings"/> as a typed value, made the first time any is asked for and kept for the whole run.<br/>
    /// Games read them through <see cref="RemoteKeys"/>, by enum number.
    /// </summary>
    internal static class RemoteValues {
        private static Dictionary<int, RemoteValue> byId;

        internal static IEnumerable<RemoteValue> All => ById.Values;

        private static Dictionary<int, RemoteValue> ById {
            get {
                if (byId == null) Build();
                return byId;
            }
        }

        /// <summary>The parameter with this enum number; throws when there is none or it is not a <typeparamref name="T"/>.</summary>
        internal static RemoteValue<T> Get<T>(int id) {
            if (!ById.TryGetValue(id, out var value)) throw new KeyNotFoundException($"{nameof(RemoteValues)}: no parameter has the number {id}");

            return Cast<T>(value);
        }

        // A play session without a domain reload builds the values again, so it takes defaults edited since the last
        // one and drops the last one's Changed handlers. RemoteKeys looks values up on each read.
        internal static void Forget() {
            byId = null;
        }

        private static RemoteValue<T> Cast<T>(RemoteValue value) {
            if (value is RemoteValue<T> typed) return typed;

            throw new InvalidCastException($"{nameof(RemoteValues)}: {value.Key} is a {value.Type} ({RemoteValue.ClrType(value.Type).Name}), not a {typeof(T).Name}");
        }

        private static void Build() {
            var values = FirebaseRemoteConfigSettings.Load().Parameters
                .Where(parameter => !string.IsNullOrEmpty(parameter.Key))
                .Select(RemoteValue.Create)
                .ToArray();
            byId = values.GroupBy(value => value.Id).ToDictionary(group => group.Key, group => group.First());
        }
    }
}