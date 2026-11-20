using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using Cysharp.Threading.Tasks;
using Google.MiniJSON;
using RiseOn.Firebase.Editor;
using UnityEngine;

namespace RiseOn.Firebase.RemoteConfig.Editor {
    /// <summary>
    /// Reads and publishes the project's Remote Config template, through the REST API with a service account or through the Firebase CLI with the developer's sign-in, as <see cref="FirebaseAccess.Mode"/> says.
    /// </summary>
    internal static class RemoteConfigConsole {
        private const string Scope = "https://www.googleapis.com/auth/firebase.remoteconfig";

        private static readonly HttpClient http = new();

        public static string ConsoleUrl(string projectId) {
            return $"https://console.firebase.google.com/project/{projectId}/config";
        }

        public static async UniTask<RemoteTemplate> FetchAsync(string projectId) {
            switch (FirebaseAccess.Mode) {
                case FirebaseAccessMode.ServiceAccount: {
                    using var request  = await RequestAsync(HttpMethod.Get, projectId, null);
                    using var response = await http.SendAsync(request);
                    var body = await ReadAsync(response);
                    var eTag = response.Headers.TryGetValues("ETag", out var values) ? values.First() : null;
                    return new RemoteTemplate(ParseObject(body), eTag);
                }
                case FirebaseAccessMode.FirebaseCli: {
                    // An empty template here would read as a console without parameters, and a push would then publish only
                    // the project's, dropping the console's other parameters and conditions.
                    var result = await FirebaseCli.RunJsonAsync($"remoteconfig:get --project {projectId}");
                    return new RemoteTemplate(result as Dictionary<string, object> ?? throw new FormatException("The Firebase CLI did not return a Remote Config template"), null);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Checks the template on the server without publishing it.<br/>
        /// The CLI cannot, so with it only the local rules run, and the server checks at publish.
        /// </summary>
        public static async UniTask ValidateAsync(string projectId, RemoteTemplate template) {
            if (FirebaseAccess.Mode is not FirebaseAccessMode.ServiceAccount) return;

            using var request  = await RequestAsync(HttpMethod.Put, projectId, template, validateOnly: true);
            using var response = await http.SendAsync(request);
            await ReadAsync(response);
        }

        /// <summary>
        /// Publishes the template.<br/>
        /// Through REST it only replaces the version it was fetched from, so a publish made in between fails instead of being overwritten; the CLI always replaces the latest.
        /// </summary>
        public static async UniTask PublishAsync(string projectId, RemoteTemplate template) {
            switch (FirebaseAccess.Mode) {
                case FirebaseAccessMode.ServiceAccount: {
                    using var request  = await RequestAsync(HttpMethod.Put, projectId, template, validateOnly: false);
                    using var response = await http.SendAsync(request);
                    await ReadAsync(response);
                    return;
                }
                case FirebaseAccessMode.FirebaseCli: {
                    var directory = Path.Combine(Path.GetTempPath(), "RiseOnRemoteConfig-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(directory);
                    try {
                        File.WriteAllText(Path.Combine(directory, "firebase.json"), "{\"remoteconfig\":{\"template\":\"template.json\"}}");
                        File.WriteAllText(Path.Combine(directory, "template.json"), template.ToFileJson(), new UTF8Encoding(false));
                        await FirebaseCli.RunJsonAsync($"deploy --only remoteconfig --project {projectId}", directory);
                    } finally {
                        Directory.Delete(directory, true);
                    }

                    return;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static async UniTask<HttpRequestMessage> RequestAsync(HttpMethod method, string projectId, RemoteTemplate template, bool validateOnly = false) {
            var token = await FirebaseAccess.LoadServiceAccount().GetAccessTokenAsync(Scope);
            var url   = $"https://firebaseremoteconfig.googleapis.com/v1/projects/{projectId}/remoteConfig{(validateOnly ? "?validateOnly=true" : "")}";
            var request = new HttpRequestMessage(method, url);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
            if (template == null) return request;

            request.Headers.TryAddWithoutValidation("If-Match", template.ETag ?? "*");
            request.Content = new StringContent(template.ToPublishJson($"Unity {Application.productName}: RiseOn Remote Config"), Encoding.UTF8, "application/json");
            return request;
        }

        private static async UniTask<string> ReadAsync(HttpResponseMessage response) {
            var body = await response.Content.ReadAsStringAsync();
            await UniTask.SwitchToMainThread();
            if (response.IsSuccessStatusCode) return body;

            var message = Json.Deserialize(body) is Dictionary<string, object> json && json.TryGetValue("error", out var error) && error is Dictionary<string, object> details
                ? details.TryGetValue("message", out var text) ? text as string : body
                : body;
            throw new InvalidOperationException((int)response.StatusCode == 412
                ? "Someone published a newer template since it was fetched. Refresh and try again."
                : $"Firebase answered {(int)response.StatusCode}: {message}");
        }

        private static Dictionary<string, object> ParseObject(string body) {
            return Json.Deserialize(body) as Dictionary<string, object> ?? throw new FormatException("Firebase sent a template that is not JSON");
        }
    }
}