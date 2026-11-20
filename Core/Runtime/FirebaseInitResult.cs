using System;
using Cysharp.Threading.Tasks;
using Firebase;

namespace RiseOn.Firebase {
    /// <summary>How one run of the startup ended: <see cref="FirebaseInitSucceeded"/> or <see cref="FirebaseInitFailed"/>.</summary>
    public abstract class FirebaseInitResult {
        private protected FirebaseInitResult() { }
    }

    public sealed class FirebaseInitSucceeded : FirebaseInitResult {
        internal FirebaseInitSucceeded() { }
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

    public sealed class FirebaseInitException : Exception {
        /// <summary>What the dependency check returned; <see cref="DependencyStatus.Available"/> when the app itself failed.</summary>
        public DependencyStatus Status { get; }

        internal FirebaseInitException(DependencyStatus status, string message, Exception inner = null) : base(message, inner) {
            Status = status;
        }
    }
}