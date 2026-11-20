using System;
using Cysharp.Threading.Tasks;
using Firebase.RemoteConfig;

namespace RiseOn.Firebase.RemoteConfig {
    /// <summary>How one launch fetch ended: <see cref="RemoteConfigInitSucceeded"/> or <see cref="RemoteConfigInitFailed"/>.</summary>
    public abstract class RemoteConfigInitResult {
        private protected RemoteConfigInitResult() { }
    }

    public sealed class RemoteConfigInitSucceeded : RemoteConfigInitResult {
        internal RemoteConfigInitSucceeded() { }
    }

    /// <summary>
    /// The fetch failed (no network, timeout, throttled) or Firebase refused the defaults, the settings or the activation.<br/>
    /// Until its owner retries or continues, everything awaiting <see cref="RemoteConfigInit.WaitUntilCompleteAsync"/> keeps waiting and every value stays its in-app default.
    /// </summary>
    public sealed class RemoteConfigInitFailed : RemoteConfigInitResult {
        private readonly Func<UniTask<RemoteConfigInitResult>> retry;
        private readonly Func<UniTask>                         proceed;
        private          bool                                  resolved;

        /// <summary>What the SDK threw; a failed fetch throws a FirebaseException without a message, see <see cref="Reason"/>.</summary>
        public Exception Error { get; }

        /// <summary>The SDK's reason when the fetch failed, <see cref="FetchFailureReason.Throttled"/> when fetches came too often; <see cref="FetchFailureReason.Error"/> for a failure that is no fetch (the defaults, the settings, the activation).</summary>
        public FetchFailureReason Reason { get; }

        internal RemoteConfigInitFailed(Exception error, FetchFailureReason reason, Func<UniTask<RemoteConfigInitResult>> retry, Func<UniTask> proceed) {
            Error        = error;
            Reason       = reason;
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