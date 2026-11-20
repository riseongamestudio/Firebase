using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using Google.MiniJSON;

namespace RiseOn.Firebase.Editor {
    /// <summary>
    /// A service account key file, the JSON the Firebase console makes in Project settings > Service accounts, and the OAuth access tokens it signs for (RFC 7523 JWT bearer grant, as Google's client libraries do).
    /// </summary>
    internal sealed class ServiceAccountKey {
        private static readonly HttpClient                                    http   = new();
        private static readonly Dictionary<string, (string token, DateTime expiry)> tokens = new();

        private readonly RSAParameters rsa;

        public string Path { get; }
        public string ClientEmail { get; }
        public string ProjectId { get; }
        private string TokenUri { get; }

        private ServiceAccountKey(string path, string clientEmail, string projectId, string tokenUri, RSAParameters rsa) {
            Path        = path;
            ClientEmail = clientEmail;
            ProjectId   = projectId;
            TokenUri    = tokenUri;
            this.rsa    = rsa;
        }

        /// <summary>Throws when the file is missing or is not a service account key.</summary>
        public static ServiceAccountKey Load(string path) {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException("No service account key file", path);

            if (Json.Deserialize(File.ReadAllText(path)) is not Dictionary<string, object> json
             || !json.TryGetValue("type", out var type) || (string)type != "service_account") {
                throw new FormatException($"{System.IO.Path.GetFileName(path)} is not a service account key");
            }

            var tokenUri = json.TryGetValue("token_uri", out var uri) ? (string)uri : "https://oauth2.googleapis.com/token";
            return new ServiceAccountKey(
                path
              , (string)json["client_email"]
              , json.TryGetValue("project_id", out var project) ? (string)project : null
              , tokenUri
              , Pkcs8.ReadRsaPem((string)json["private_key"]));
        }

        /// <summary>An access token for <paramref name="scope"/>, reused until a minute before it expires.</summary>
        public async UniTask<string> GetAccessTokenAsync(string scope) {
            var cacheKey = ClientEmail + " " + scope;
            if (tokens.TryGetValue(cacheKey, out var cached) && cached.expiry > DateTime.UtcNow.AddMinutes(1)) return cached.token;

            var form = new FormUrlEncodedContent(new Dictionary<string, string> {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer"
              , ["assertion"]  = SignAssertion(scope)
            });
            using var response = await http.PostAsync(TokenUri, form);
            var body = await response.Content.ReadAsStringAsync();
            await UniTask.SwitchToMainThread();

            var json = Json.Deserialize(body) as Dictionary<string, object>;
            if (!response.IsSuccessStatusCode || json == null || !json.ContainsKey("access_token")) {
                var reason = json != null && json.TryGetValue("error_description", out var description) ? description : body;
                throw new InvalidOperationException($"Google refused the service account {ClientEmail}: {reason}");
            }

            var token  = (string)json["access_token"];
            var expiry = DateTime.UtcNow.AddSeconds(Convert.ToDouble(json["expires_in"]));
            tokens[cacheKey] = (token, expiry);
            return token;
        }

        private string SignAssertion(string scope) {
            var now    = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var header = Base64Url(Encoding.UTF8.GetBytes(Json.Serialize(new Dictionary<string, object> { ["alg"] = "RS256", ["typ"] = "JWT" })));
            var claims = Base64Url(Encoding.UTF8.GetBytes(Json.Serialize(new Dictionary<string, object> {
                ["iss"]   = ClientEmail
              , ["scope"] = scope
              , ["aud"]   = TokenUri
              , ["iat"]   = now
              , ["exp"]   = now + 3600
            })));

            var signed = header + "." + claims;
            using var key = RSA.Create();
            key.ImportParameters(rsa);
            return signed + "." + Base64Url(key.SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }

        private static string Base64Url(byte[] bytes) {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}