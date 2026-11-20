using System;
using UnityEngine;

namespace RiseOn.Firebase.RemoteConfig {
    internal enum RemoteValueType {
        Bool
      , Int
      , Float
      , String
      , Json
    }

    /// <summary>One Remote Config parameter as the project declares it: the key, its type and its in-app default.</summary>
    [Serializable]
    internal sealed class RemoteParameter {
        [SerializeField]
        [Tooltip("The number of the parameter's enum member: random, so parameters two branches add do not meet on one number, and kept, so enum fields keep their value when keys are renamed or removed.")]
        private int id;

        [SerializeField]
        [Tooltip("The key in the Firebase console: a letter or underscore, then letters, digits or underscores.")]
        private string key = "";

        [SerializeField]
        private RemoteValueType type;

        [SerializeField]
        [Tooltip("The in-app default, used until the first fetch activates, when a fetch fails, and for keys the console does not have. Written in invariant culture.")]
        private string defaultValue = "";

        [SerializeField]
        private string description = "";

        [SerializeField]
        [Tooltip("The parameter group in the Firebase console; empty for none.")]
        private string group = "";

        [SerializeField]
        [Tooltip("Takes a value published while the game runs; otherwise the value stays the one of launch until the next launch.")]
        private bool live;

        public int Id { get => id; internal set => id = value; }
        public string Key { get => key; set => key = value; }
        public RemoteValueType Type { get => type; set => type = value; }
        public string DefaultValue { get => defaultValue; set => defaultValue = value; }
        public string Description { get => description; set => description = value; }
        public string Group { get => group; set => group = value; }
        public bool Live { get => live; set => live = value; }
    }
}