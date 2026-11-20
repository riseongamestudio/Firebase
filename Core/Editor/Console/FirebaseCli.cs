using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Google.MiniJSON;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RiseOn.Firebase.Editor {
    /// <summary>What <c>firebase login:list</c> says: the CLI's version and the Google account it signed in with.</summary>
    internal sealed class FirebaseCliStatus {
        /// <summary>Null when the CLI is not installed or not on the PATH the Editor sees.</summary>
        public string Version { get; }

        /// <summary>Null when the CLI has no signed-in account.</summary>
        public string Account { get; }

        public bool IsInstalled => Version != null;
        public bool IsSignedIn => Account != null;

        internal FirebaseCliStatus(string version, string account) {
            Version = version;
            Account = account;
        }
    }

    internal sealed class FirebaseCliException : Exception {
        internal FirebaseCliException(string message) : base(message) { }
    }

    /// <summary>
    /// Runs the Firebase CLI (firebase-tools) the developer installed and signed in with <c>firebase login</c>, which keeps its own Google sign-in; the package keeps no Google credentials of its own.
    /// </summary>
    internal static class FirebaseCli {
        private static readonly Regex jsonStart = new("^{", RegexOptions.Multiline);

        /// <summary>Runs <c>firebase &lt;arguments&gt; --json</c> and returns the <c>result</c> it prints.</summary>
        public static async UniTask<object> RunJsonAsync(string arguments, string workingDirectory = null) {
            var (exitCode, output, error) = await RunAsync(arguments + " --json --non-interactive", workingDirectory);

            // The CLI may print notices before the JSON, which starts a line of its own.
            var begin = jsonStart.Match(output);
            if (!begin.Success || Json.Deserialize(output.Substring(begin.Index)) is not Dictionary<string, object> json) {
                throw new FirebaseCliException(exitCode == 9009 || error.Contains("not recognized") || error.Contains("not found")
                    ? "the Firebase CLI is not installed or not on the PATH"
                    : $"firebase {arguments} failed ({FirstLine(error, output)})");
            }

            if (!json.TryGetValue("status", out var status) || (string)status != "success") {
                throw new FirebaseCliException($"firebase {arguments}: {(json.TryGetValue("error", out var message) ? message : FirstLine(error, output))}");
            }

            return json.TryGetValue("result", out var result) ? result : null;
        }

        /// <summary>What the last <see cref="RefreshStatusAsync"/> found; null before the first one ends.</summary>
        public static FirebaseCliStatus LastStatus { get; private set; }

        public static bool IsRefreshingStatus { get; private set; }

        /// <summary>Asks the CLI for its version and account; settings pages show <see cref="LastStatus"/> meanwhile.</summary>
        public static async UniTask<FirebaseCliStatus> RefreshStatusAsync() {
            IsRefreshingStatus = true;
            try {
                return LastStatus = await GetStatusAsync();
            } finally {
                IsRefreshingStatus = false;
            }
        }

        private static async UniTask<FirebaseCliStatus> GetStatusAsync() {
            var (exitCode, version, _) = await RunAsync("--version");
            if (exitCode != 0) return new FirebaseCliStatus(null, null);

            try {
                var accounts = await RunJsonAsync("login:list") as List<object>;
                var account = accounts?
                    .OfType<Dictionary<string, object>>()
                    .Select(entry => entry.TryGetValue("user", out var user) ? user as Dictionary<string, object> : null)
                    .Select(user => user != null && user.TryGetValue("email", out var email) ? email as string : null)
                    .FirstOrDefault(email => email != null);
                return new FirebaseCliStatus(version.Trim(), account);
            } catch (FirebaseCliException) {
                return new FirebaseCliStatus(version.Trim(), null);
            }
        }

        /// <summary>Opens a terminal running <c>firebase login --reauth</c>, which signs in through the browser.</summary>
        public static void OpenSignIn() {
            switch (Application.platform) {
                case RuntimePlatform.WindowsEditor:
                    Process.Start(new ProcessStartInfo("cmd.exe", "/k firebase login --reauth") { UseShellExecute = true });
                    break;
                case RuntimePlatform.OSXEditor:
                    Process.Start("osascript", "-e \"tell application \\\"Terminal\\\" to do script \\\"firebase login --reauth\\\"\" -e \"tell application \\\"Terminal\\\" to activate\"");
                    break;
                case RuntimePlatform.LinuxEditor:
                    Process.Start("x-terminal-emulator", "-e firebase login --reauth");
                    break;
                default:
                    Debug.LogError($"{nameof(FirebaseCli)}: run 'firebase login' in a terminal");
                    break;
            }
        }

        private static async UniTask<(int exitCode, string output, string error)> RunAsync(string arguments, string workingDirectory = null) {
            var info = Application.platform switch {
                // npm installs firebase as a .cmd shim, which only cmd resolves.
                RuntimePlatform.WindowsEditor => new ProcessStartInfo("cmd.exe", $"/d /s /c \"firebase {arguments}\"")
                // A login shell, for the PATH that Homebrew, nvm or npm set up.
              , RuntimePlatform.OSXEditor   => new ProcessStartInfo("/bin/zsh", $"-lc \"firebase {arguments.Replace("\"", "\\\"")}\"")
              , RuntimePlatform.LinuxEditor => new ProcessStartInfo("/bin/bash", $"-lc \"firebase {arguments.Replace("\"", "\\\"")}\"")
              , _                           => throw new PlatformNotSupportedException(Application.platform.ToString())
            };
            info.UseShellExecute        = false;
            info.CreateNoWindow         = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError  = true;
            info.StandardOutputEncoding = Encoding.UTF8;
            info.StandardErrorEncoding  = Encoding.UTF8;
            info.WorkingDirectory       = workingDirectory ?? Environment.CurrentDirectory;
            info.EnvironmentVariables["NO_UPDATE_NOTIFIER"] = "1";

            using var process = new Process { StartInfo = info };
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync();
            var error  = process.StandardError.ReadToEndAsync();
            await UniTask.RunOnThreadPool(() => process.WaitForExit());
            var result = (process.ExitCode, await output, await error);
            await UniTask.SwitchToMainThread();
            return result;
        }

        private static string FirstLine(params string[] texts) {
            return texts
                .SelectMany(text => text.Split('\n'))
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0) ?? "no output";
        }
    }
}