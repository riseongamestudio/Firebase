using System;
using Cysharp.Threading.Tasks;

namespace RiseOn.Firebase.RemoteConfig {
    /// <summary>How one launch fetch ended: <see cref="RemoteConfigInitSucceeded"/>, <see cref="RemoteConfigInitFailed"/> or <see cref="RemoteConfigInitUnsupported"/>.</summary>
    public abstract class RemoteConfigInitResult {
        private protected RemoteConfigInitResult() { }
    }

    public sealed class RemoteConfigInitSucceeded : RemoteConfigInitResult {
        internal RemoteConfigInitSucceeded() { }
    }

    /// <summary>Firebase does not run on this platform (see <see cref="FirebaseInit.IsSupported"/>): nothing was fetched, every value keeps its in-app default.</summary>
    public sealed class RemoteConfigInitUnsupported : RemoteConfigInitResult {
        internal RemoteConfigInitUnsupported() { }
    }

    /// <summary>
    /// The fetch failed (no network, timeout, throttled) or Firebase refused the defaults, the settings or the activation.<br/>
    /// Until its owner retries or continues, everything awaiting <see cref="RemoteConfigInit.WaitUntilCompleteAsync"/> keeps waiting and every value stays its in-app default.
    /// </summary>
    public sealed class RemoteConfigInitFailed : RemoteConfigInitResult {
        private readonly Func<UniTask<RemoteConfigInitResult>> retry;
        private readonly Func<UniTask>                         proceed;
        private          bool                                  resolved;

        /// <summary>What the SDK threw; a failed fetch throws a FirebaseException without a message, and the error <see cref="RemoteConfigInit"/> logs names the SDK's reason.</summary>
        public Exception Error { get; }

        /// <summary>Firebase refused the fetch because fetches came too often: a retry is refused too, until the interval passes.</summary>
        public bool Throttled { get; }

        internal RemoteConfigInitFailed(Exception error, bool throttled, Func<UniTask<RemoteConfigInitResult>> retry, Func<UniTask> proceed) {
            Error        = error;
            Throttled    = throttled;
            this.retry   = retry;
            this.proceed = proceed;
        }

        /// <summary>Fetches again; the result belongs to the caller and is not handed out by <see cref="RemoteConfigInit.PopResultAsync"/>.</summary>
        public UniTask<RemoteConfigInitResult> RetryAsync() {
            Resolve();
            return retry();
        }

        /// <summary>Completes with the values the device has fetched, the in-app defaults for keys without one; the next launch fetches again.</summary>
        public UniTask ContinueAsync() {
            Resolve();
            return proceed();
        }

        private void Resolve() {
            if (resolved) throw new InvalidOperationException($"{nameof(RemoteConfigInitFailed)}: this failure was already retried or continued");
            resolved = true;
        }
    }
}