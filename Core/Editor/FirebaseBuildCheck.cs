using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RiseOn.Firebase.Editor {
    /// <summary>
    /// Stops a build that would ship without a working Firebase: a config file that is missing or made for another app, settings the game cannot load, or App Check on without its package.<br/>
    /// A build for another platform goes through with a warning: Firebase stands still there (see <see cref="FirebaseInit.IsSupported"/>).<br/>
    /// The settings page shows the same problems.
    /// </summary>
    internal sealed class FirebaseBuildCheck : IPreprocessBuildWithReport {
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report) {
            var target = report.summary.platform;
            // Firebase's desktop SDK is a beta for development workflows, not for shipping; WebGL has no SDK at all.
            if (target is not BuildTarget.Android and not BuildTarget.iOS) {
                Debug.LogWarning($"Firebase: com.riseon.firebase runs on Android and iOS only; in this {target} build it logs an error at launch and stands still.");
                return;
            }

            if (FirebaseSettingsProvider.OutsideResources() is { } outside) throw new BuildFailedException($"Firebase: {outside} Move it in Project Settings > RiseOn > Firebase.");

            var problems = FirebaseProjectFiles.Problems(target).Select(problem => problem.Message).ToList();
            if (problems.Count > 0) throw new BuildFailedException("Firebase: fix the config file before building (Project Settings > RiseOn > Firebase):\n- " + string.Join("\n- ", problems));
#if !FIREBASE_SUPPORT_APPCHECK
            if (FirebaseSettingsProvider.Find().AppCheck) {
                throw new BuildFailedException($"Firebase: App Check is on but {FirebaseSettingsProvider.AppCheckPackage} is not installed; install it in Project Settings > RiseOn > Firebase, or turn App Check off.");
            }
#endif
        }
    }
}