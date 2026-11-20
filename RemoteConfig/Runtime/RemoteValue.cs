using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RiseOn.Firebase.RemoteConfig {
    /// <summary>A parameter's current value, without its type; see <see cref="RemoteValue{T}"/>.</summary>
    public abstract class RemoteValue {
        internal int Id { get; }
        internal string Key { get; }
        internal RemoteValueType Type { get; }

        /// <summary>Takes values published while the game runs; otherwise it keeps the value of launch.</summary>
        internal bool IsLive { get; }

        private protected RemoteValue(RemoteParameter parameter) {
            Id     = parameter.Id;
            Key    = parameter.Key;
            Type   = parameter.Type;
            IsLive = parameter.Live;
        }

        /// <summary>Takes the value Firebase holds, as text; null goes back to the in-app default.</summary>
        internal abstract void Set(string text);

        internal static Type ClrType(RemoteValueType type) {
            switch (type) {
                case RemoteValueType.Bool:   return typeof(bool);
                case RemoteValueType.Int:    return typeof(int);
                case RemoteValueType.Float:  return typeof(float);
                case RemoteValueType.String: return typeof(string);
                case RemoteValueType.Json:   return typeof(string);
                default:                     throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        internal static RemoteValue Create(RemoteParameter parameter) {
            switch (parameter.Type) {
                case RemoteValueType.Bool:   return new RemoteValue<bool>(parameter, ParseBool);
                case RemoteValueType.Int:    return new RemoteValue<int>(parameter, ParseInt);
                case RemoteValueType.Float:  return new RemoteValue<float>(parameter, ParseFloat);
                case RemoteValueType.String: return new RemoteValue<string>(parameter, ParseString);
                case RemoteValueType.Json:   return new RemoteValue<string>(parameter, ParseString);
                default:                     throw new ArgumentOutOfRangeException(nameof(parameter.Type), parameter.Type, null);
            }
        }

        // The console stores every value as text and its BOOLEAN type writes true or false; the other spellings cover
        // parameters made as STRING.
        internal static bool ParseBool(string text, out bool value) {
            switch (text.Trim().ToLowerInvariant()) {
                case "1": case "true": case "t": case "yes": case "y": case "on":
                    value = true;
                    return true;
                case "0": case "false": case "f": case "no": case "n": case "off": case "":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        internal static bool ParseInt(string text, out int value) {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;

            // The console's NUMBER type allows "5.0" for an int parameter.
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && Math.Abs(number % 1) < double.Epsilon
             && number is >= int.MinValue and <= int.MaxValue) {
                value = (int)number;
                return true;
            }

            return false;
        }

        internal static bool ParseFloat(string text, out float value) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        internal static bool ParseString(string text, out string value) {
            value = text;
            return true;
        }
    }

    /// <summary>
    /// A parameter's current value.<br/>
    /// Until <see cref="RemoteConfigInit"/> completes it is the in-app default; then it is the value of launch, and for live parameters each value published later.
    /// </summary>
    public sealed class RemoteValue<T> : RemoteValue {
        internal delegate bool Parser(string text, out T value);

        private readonly Parser parse;

        public T Value { get; private set; }
        public T DefaultValue { get; }

        /// <summary>Unity's thread. The value changed: at launch when it differs from the default, and for live parameters when one is published.</summary>
        public event Action<T> Changed;

        internal RemoteValue(RemoteParameter parameter, Parser parse) : base(parameter) {
            this.parse = parse;
            if (!parse(parameter.DefaultValue ?? "", out var defaultValue)) {
                Debug.LogError($"{nameof(RemoteValue)}: the default of {Key} ({parameter.DefaultValue}) is not a {Type}");
            }

            DefaultValue = defaultValue;
            Value        = defaultValue;
        }

        internal override void Set(string text) {
            var value = DefaultValue;
            if (text != null && !parse(text, out value)) {
                Debug.LogWarning($"{nameof(RemoteValue)}: {Key} got \"{text}\", which is not a {Type}; it keeps the default");
                value = DefaultValue;
            }

            if (EqualityComparer<T>.Default.Equals(value, Value)) return;

            Value = value;
            Changed?.Invoke(value);
        }

        public static implicit operator T(RemoteValue<T> remote) => remote.Value;

        public override string ToString() => $"{Key} = {Value}";
    }
}