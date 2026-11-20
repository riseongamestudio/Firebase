namespace RiseOn.Firebase.RemoteConfig {
    /// <summary>
    /// A typed property per key of Project Settings > RiseOn > Firebase > Remote Config, which the pack's source generator compiles in from that page, named by the key as written: <c>ads_interval</c> is <c>RemoteKeys.ads_interval</c>.<br/>
    /// It has no member of its own, so no key is kept from it.
    /// </summary>
    public static partial class RemoteKeys { }

    public static class RemoteKeyUtils {
        /// <summary>The value of a key chosen in the Inspector; throws when it is not a <typeparamref name="T"/>.</summary>
        public static RemoteValue<T> Get<T>(this RemoteKey key) => RemoteValues.Get<T>((int)key);
    }
}