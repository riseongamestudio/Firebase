using System.Collections.Generic;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Firebase.RemoteConfig {
    /// <summary>The loading strategies of Firebase's Remote Config docs that a game with a boot screen can use.</summary>
    internal enum RemoteConfigLoading {
        /// <summary>Fetch and activate before the values of launch; the boot screen waits up to the fetch timeout.</summary>
        WaitForFetch
      , /// <summary>Activate what the last launch fetched, then fetch in the background for the next launch.</summary>
        NextLaunch
    }

    /// <summary>
    /// The project's Remote Config parameters and fetch settings, edited in Project Settings > RiseOn > Firebase > Remote Config and kept in a Resources asset that <see cref="RemoteConfigInit"/> reads at launch.
    /// </summary>
    internal sealed class FirebaseRemoteConfigSettings : ScriptableObject {
        public const string ResourceName = "RiseOnRemoteConfigSettings";

        [SerializeField]
        private List<RemoteParameter> parameters = new();

        [SerializeField, SectionGroup("Fetch", Tooltip = "When the game asks the console for new values, and how long it waits.")]
        [Tooltip("WaitForFetch: the launch waits for a fetch, which Firebase strongly recommends for A/B tests. NextLaunch: the launch takes the values fetched last time at once and fetches for the next one, which Firebase's fetch best practices recommend otherwise.")]
        private RemoteConfigLoading loading = RemoteConfigLoading.WaitForFetch;

        [SerializeField, SectionGroup("Fetch"), LabelText("Timeout (s)")]
        [MinValue(1)]
        [Tooltip("How long the launch fetch may take before it fails. Firebase's loading strategies page calls the SDK's own 60 seconds too long for a loading screen; 3 is the cap Firebase's Remote Config V2 post uses.")]
        private int fetchTimeoutSeconds = 3;

        [SerializeField, SectionGroup("Fetch"), LabelText("Interval (min)")]
        [MinValue(0)]
        [Tooltip("A fetch sooner than this after the last one returns the cached values without calling the server. 12 hours is the SDK default, which Firebase's Unity guide calls a good number for production; much shorter risks throttling.")]
        private int minimumFetchIntervalMinutes = 720;

        [SerializeField, SectionGroup("Fetch"), LabelText("Dev Interval (min)")]
        [MinValue(0)]
        [Tooltip("The same, for development builds and the Editor. Firebase's samples use 0 during development so console changes show at once, and say to raise it before shipping.")]
        private int developmentMinimumFetchIntervalMinutes;

        public List<RemoteParameter> Parameters => parameters;
        public RemoteConfigLoading Loading => loading;
        public int FetchTimeoutSeconds => fetchTimeoutSeconds;
        public int MinimumFetchIntervalMinutes => minimumFetchIntervalMinutes;
        public int DevelopmentMinimumFetchIntervalMinutes => developmentMinimumFetchIntervalMinutes;

        /// <summary>The project's asset, or an empty one when the project never saved one.</summary>
        public static FirebaseRemoteConfigSettings Load() {
            var asset = Resources.Load<FirebaseRemoteConfigSettings>(ResourceName);
            return asset ? asset : CreateInstance<FirebaseRemoteConfigSettings>();
        }
    }
}