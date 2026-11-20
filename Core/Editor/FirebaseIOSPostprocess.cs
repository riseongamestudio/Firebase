#if UNITY_IOS && FIREBASE_SUPPORT_APPCHECK
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace RiseOn.Firebase.Editor {
    /// <summary>
    /// Gives the app the App Attest entitlement when App Check uses App Attest on iOS, the entitlement Xcode adds with the App Attest capability.<br/>
    /// Development builds use the debug provider, so the environment is production.<br/>
    /// It runs after the other build steps it can, in the entitlements file they share, and stops the build when one of them set another environment.
    /// </summary>
    internal static class FirebaseIOSPostprocess {
        private const string AppAttestKey        = "com.apple.developer.devicecheck.appattest-environment";
        private const string DefaultEntitlements = "Unity-iPhone/Unity-iPhone.entitlements";
        private const string Environment         = "production";

        [PostProcessBuild(int.MaxValue)]
        private static void OnPostprocessBuild(BuildTarget target, string buildPath) {
            if (target is not BuildTarget.iOS) return;

            var settings = FirebaseSettingsProvider.Find();
            if (!settings.AppCheck || settings.AppCheckIOSProvider is not AppCheckIOSProvider.AppAttest) return;

            var projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project     = new PBXProject();
            project.ReadFromFile(projectPath);

            // Shares the entitlements file another plugin already set, as Xcode keeps one per target.
            var mainTarget   = project.GetUnityMainTargetGuid();
            var entitlements = project.GetBuildPropertyForAnyConfig(mainTarget, "CODE_SIGN_ENTITLEMENTS");
            if (string.IsNullOrEmpty(entitlements)) {
                entitlements = DefaultEntitlements;
                project.AddFile(entitlements, entitlements);
                project.SetBuildProperty(mainTarget, "CODE_SIGN_ENTITLEMENTS", entitlements);
            }

            var path  = Path.Combine(buildPath, entitlements);
            var plist = new PlistDocument();
            if (File.Exists(path)) plist.ReadFromFile(path);
            if (plist.root.values.TryGetValue(AppAttestKey, out var existing) && (existing as PlistElementString)?.value is not Environment) {
                throw new BuildFailedException($"{nameof(FirebaseIOSPostprocess)}: another build step set {AppAttestKey} in {entitlements} to {(existing as PlistElementString)?.value ?? existing.GetType().Name}; App Check needs {Environment}, remove it there");
            }

            plist.root.SetString(AppAttestKey, Environment);
            plist.WriteToFile(path);
            project.WriteToFile(projectPath);
        }
    }
}
#endif