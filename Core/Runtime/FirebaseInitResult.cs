using System;
using Cysharp.Threading.Tasks;

namespace RiseOn.Firebase {
    /// <summary>How one run of the startup ended: <see cref="FirebaseInitSucceeded"/>, <see cref="FirebaseInitFailed"/> or <see cref="FirebaseInitUnsupported"/>.</summary>
    public abstract class FirebaseInitResult {
        private protected FirebaseInitResult() { }
    }

    public sealed class FirebaseInitSucceeded : FirebaseInitResult {
        internal FirebaseInitSucceeded() { }
    }

    /// <summary>
    /// Firebase does not run on this platform (see <see cref="FirebaseInit.IsSupported"/>): nothing started, and <see cref="FirebaseInit.WaitUntilCompleteAsync"/> completed at once so that nothing waits on it.<br/>
    /// The packages that use Firebase stand still: Remote Config keeps the in-app defaults, Analytics sends nothing.
    /// </summary>
    public sealed class FirebaseInitUnsupported : FirebaseInitResult {
        internal FirebaseInitUnsupported() { }
    }

    /// <summary>
    /// The settings could not be applied, the dependency check failed (Google Play services missing, disabled or out of date where a module needs them), or the app could not be created.<br/>
    /// A game that uses Firebase is broken without it, so there is no going on: until a retry succeeds, everything awaiting <see cref="FirebaseInit.WaitUntilCompleteAsync"/> keeps waiting.
    /// </summary>
    public sealed class FirebaseInitFailed : FirebaseInitResult {
        private readonly Func<UniTask<FirebaseInitResult>> retry;
        private          bool                              retried;

        public FirebaseInitException Error { get; }

        internal FirebaseInitFailed(FirebaseInitException error, Func<UniTask<FirebaseInitResult>> retry) {
            Error      = error;
            this.retry = retry;
        }

        /// <summary>
        /// Checks the dependencies again, asking the player to update or enable Google Play services if needed; the result belongs to the caller and is not handed out by <see cref="FirebaseInit.PopResultAsync"/>.
        /// </summary>
        public UniTask<FirebaseInitResult> RetryAsync() {
            if (retried) throw new InvalidOperationException($"{nameof(FirebaseInitFailed)}: this failure was already retried");

            retried = true;
            return retry();
        }
    }

    /// <summary>Its message says which step failed and, after the dependency check, what the check returned.</summary>
    public sealed class FirebaseInitException : Exception {
        internal FirebaseInitException(string message, Exception inner = null) : base(message, inner) { }
    }
}