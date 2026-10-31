using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace kok {
    public sealed class McpOAuthToken {
        public string access_token = "", refresh_token = "", token_type = "Bearer", scope = "", resource = "", client_id = "", client_secret = "", issuer = "", authorization_endpoint = "", token_endpoint = "", registration_endpoint = "";
        public long expires_at;
    }

    public sealed class McpOAuthFlow : IDisposable {
        internal TcpListener listener;
        internal Task<McpOAuthToken> completion;
        public string AuthorizeUrl { get; internal set; }
        public string RedirectUri { get; internal set; }
        public string State { get; internal set; }
        public Task<McpOAuthToken> Completion { get { return completion; } }
        public void Dispose() { try { if (listener != null) listener.Stop(); } catch { } listener = null; }
    }

    // OAuth 2.1 authorization-code + PKCE for remote MCP servers: protected-resource metadata
    // (RFC 9728), authorization-server metadata (RFC 8414), dynamic client registration (RFC 7591),
    // a loopback redirect and refresh tokens. Tokens are stored inside the DPAPI-encrypted MCP config.
    internal static class McpOAuth {
        const string Key = "kok.mcp.oauth.v1";
        const int CallbackTimeoutSeconds = 300;
        static readonly HttpClient http = CreateHttp();

        static HttpClient CreateHttp() {
            var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        }

        internal static string Protect(McpOAuthToken token) {
            if (token == null || String.IsNullOrEmpty(token.access_token)) throw new AgentFailure("mcp_auth", "OAuth 令牌无效，请重新登录。");
            string json = Library.Json.Serialize(token);
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(json), Encoding.UTF8.GetBytes(Key), DataProtectionScope.CurrentUser));
        }
        internal static McpOAuthToken Read(McpServerConfig server) {
            if (server == null || String.IsNullOrEmpty(server.protectedOAuth)) return null;
            try {
                string json = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(server.protectedOAuth), Encoding.UTF8.GetBytes(Key), DataProtectionScope.CurrentUser));
                var token = Library.Json.Deserialize<McpOAuthToken>(json);
                if (token == null || String.IsNullOrEmpty(token.access_token) || token.access_token.Length > 8192) throw new InvalidDataException();
                return token;
            } catch { throw new AgentFailure("mcp_auth", "OAuth 登录记录无法解密，请重新登录。"); }
        }
        internal static string AccessToken(McpServerConfig server) {
            var token = Read(server);
            if (token == null) throw new AgentFailure("mcp_auth", "请先在 MCP 设置中完成 OAuth 登录。");
            return token.access_token;
        }
        internal static bool ExpiredSoon(McpOAuthToken token) { return token != null && token.expires_at > 0 && token.expires_at <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60; }
        internal static bool CanRefresh(McpServerConfig server) { try { var token = Read(server); return token != null && !String.IsNullOrEmpty(token.refresh_token) && !String.IsNullOrEmpty(token.token_endpoint); } catch { return false; } }

        internal static string Challenge(string verifier) { return Base64Url(SHA256.Create().ComputeHash(Encoding.ASCII.GetBytes(verifier))); }
        static string Base64Url(byte[] bytes) { return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
        static string RandomValue(int bytes) { var buffer = new byte[bytes]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(buffer); return Base64Url(buffer); }

        internal static async Task<McpOAuthFlow> Begin(McpServerConfig server, Action<string> openBrowser, CancellationToken lifetime) {
            if (server == null || server.auth != "oauth" || (server.transport ?? "stdio") != "http") throw new AgentFailure("mcp_oauth", "OAuth 只适用于远程 HTTP MCP 服务。");
            var resource = McpHttpClient.ValidateEndpoint(server.url);
            var protectedResource = await DiscoverProtectedResource(resource, lifetime);
            var issuer = FirstString(protectedResource, "authorization_servers");
            if (String.IsNullOrEmpty(issuer)) throw new AgentFailure("mcp_oauth", "MCP 服务没有公布 OAuth 授权服务器。");
            var metadata = await DiscoverAuthorizationServer(issuer, lifetime);
            string authorize = Str(metadata, "authorization_endpoint"), tokenEndpoint = Str(metadata, "token_endpoint"), registration = Str(metadata, "registration_endpoint");
            ValidateEndpoint(authorize, "authorization_endpoint");
            ValidateEndpoint(tokenEndpoint, "token_endpoint");
            if (registration.Length == 0) throw new AgentFailure("mcp_oauth", "MCP 授权服务器不支持动态客户端注册，0.9.2 暂不支持此服务器。");
            ValidateEndpoint(registration, "registration_endpoint");
            var methods = metadata.ContainsKey("code_challenge_methods_supported") ? Library.Json.Deserialize<List<string>>(Library.Json.Serialize(metadata["code_challenge_methods_supported"])) : null;
            if (methods != null && !methods.Contains("S256")) throw new AgentFailure("mcp_oauth", "MCP 授权服务器不支持 PKCE S256。");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            string redirectUri = "http://127.0.0.1:" + port + "/callback";
            try {
                string clientId, clientSecret;
                RegisterClient(registration, redirectUri, out clientId, out clientSecret, lifetime);
                if (String.IsNullOrEmpty(clientId)) throw new AgentFailure("mcp_oauth", "MCP OAuth 客户端注册失败。");
                string state = RandomValue(24), verifier = RandomValue(48), challenge = Challenge(verifier);
                string scope = String.Join(" ", StrList(protectedResource, "scopes_supported"));
                var query = new List<string> {
                    "response_type=code", "client_id=" + Escape(clientId), "redirect_uri=" + Escape(redirectUri),
                    "code_challenge=" + Escape(challenge), "code_challenge_method=S256", "state=" + Escape(state), "resource=" + Escape(resource.AbsoluteUri)
                };
                if (scope.Length > 0) query.Insert(3, "scope=" + Escape(scope));
                var authorizeUri = new UriBuilder(authorize) { Query = String.Join("&", query) }.Uri;
                var flow = new McpOAuthFlow { listener = listener, AuthorizeUrl = authorizeUri.AbsoluteUri, RedirectUri = redirectUri, State = state };
                var metadataToken = new McpOAuthToken { client_id = clientId, client_secret = clientSecret, issuer = Str(metadata, "issuer"), authorization_endpoint = authorize, token_endpoint = tokenEndpoint, registration_endpoint = registration, resource = resource.AbsoluteUri, token_type = "Bearer" };
                flow.completion = CompleteAsync(flow, metadataToken, verifier, lifetime);
                if (openBrowser != null) { try { openBrowser(flow.AuthorizeUrl); } catch { } }
                return flow;
            } catch { try { listener.Stop(); } catch { } throw; }
        }

        static async Task<McpOAuthToken> CompleteAsync(McpOAuthFlow flow, McpOAuthToken template, string verifier, CancellationToken lifetime) {
            try {
                var acceptTask = flow.listener.AcceptTcpClientAsync();
                var timeout = Task.Delay(TimeSpan.FromSeconds(CallbackTimeoutSeconds), lifetime);
                if (await Task.WhenAny(acceptTask, timeout) != acceptTask) { lifetime.ThrowIfCancellationRequested(); throw new AgentFailure("mcp_oauth_timeout", "OAuth 登录等待超时，请重试。"); }
                using (var client = await acceptTask) using (var stream = client.GetStream()) {
                    string requestLine = await ReadRequestLine(stream, lifetime);
                    string target = requestLine.Split(' ').Length > 1 ? requestLine.Split(' ')[1] : "";
                    var uri = new Uri("http://127.0.0.1" + (target.StartsWith("/") ? target : "/"));
                    var query = ParseQuery(uri.Query);
                    string state, code, error;
                    query.TryGetValue("state", out state); query.TryGetValue("code", out code); query.TryGetValue("error", out error);
                    string body = "<html><meta charset=\"utf-8\"><body style=\"font-family:Segoe UI;padding:40px\"><h2>kok MCP 登录</h2><p>可以关闭此页面并返回 kok。</p></body></html>";
                    byte[] html = Encoding.UTF8.GetBytes(body);
                    string header = "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + html.Length + "\r\nConnection: close\r\n\r\n";
                    byte[] head = Encoding.ASCII.GetBytes(header);
                    await stream.WriteAsync(head, 0, head.Length); await stream.WriteAsync(html, 0, html.Length);
                    if (error != null || uri.AbsolutePath != "/callback") throw new AgentFailure("mcp_auth", "OAuth 登录被拒绝或已取消。");
                    if (state != flow.State) throw new AgentFailure("mcp_auth", "OAuth 回调 state 不匹配，登录已取消。");
                    if (String.IsNullOrEmpty(code) || code.Length > 8192) throw new AgentFailure("mcp_auth", "OAuth 回调缺少授权码。");
                    return await Exchange(template, code, flow.RedirectUri, verifier, lifetime);
                }
            } finally { flow.Dispose(); }
        }

        static async Task<string> ReadRequestLine(Stream stream, CancellationToken token) {
            var bytes = new List<byte>();
            while (bytes.Count < 16384) {
                int value = stream.ReadByte();
                if (value < 0) break;
                if (value == '\n') break;
                if (value != '\r') bytes.Add((byte)value);
                token.ThrowIfCancellationRequested();
            }
            return Encoding.ASCII.GetString(bytes.ToArray());
        }

        internal static async Task<McpOAuthToken> Exchange(McpOAuthToken template, string code, string redirectUri, string verifier, CancellationToken token) {
            var form = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string,string>("grant_type","authorization_code"),
                new KeyValuePair<string,string>("code",code),
                new KeyValuePair<string,string>("redirect_uri",redirectUri),
                new KeyValuePair<string,string>("code_verifier",verifier),
                new KeyValuePair<string,string>("resource",template.resource)
            };
            if (String.IsNullOrEmpty(template.client_secret)) form.Add(new KeyValuePair<string,string>("client_id",template.client_id));
            return await TokenRequest(template, form, token);
        }

        internal static async Task<McpOAuthToken> Refresh(McpServerConfig server) {
            var token = Read(server);
            if (token == null || String.IsNullOrEmpty(token.refresh_token)) throw new AgentFailure("mcp_auth", "OAuth 登录已过期，请重新登录。");
            var form = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string,string>("grant_type","refresh_token"),
                new KeyValuePair<string,string>("refresh_token",token.refresh_token),
                new KeyValuePair<string,string>("resource",token.resource)
            };
            if (String.IsNullOrEmpty(token.client_secret)) form.Add(new KeyValuePair<string,string>("client_id",token.client_id));
            var updated = await TokenRequest(token, form, CancellationToken.None);
            if (String.IsNullOrEmpty(updated.refresh_token)) updated.refresh_token = token.refresh_token;
            return updated;
        }

        static async Task<McpOAuthToken> TokenRequest(McpOAuthToken template, List<KeyValuePair<string, string>> form, CancellationToken token) {
            using (var request = new HttpRequestMessage(HttpMethod.Post, template.token_endpoint)) {
                request.Content = new FormUrlEncodedContent(form);
                if (!String.IsNullOrEmpty(template.client_secret)) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(template.client_id + ":" + template.client_secret)));
                HttpResponseMessage response;
                try { response = await http.SendAsync(request, token); } catch (OperationCanceledException) { throw new AgentFailure("mcp_oauth_timeout", "OAuth 令牌请求超时。"); } catch (HttpRequestException) { throw new AgentFailure("mcp_oauth", "无法连接 OAuth 令牌端点。"); }
                using (response) {
                    string text = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode) throw new AgentFailure("mcp_auth", "OAuth 令牌请求被拒绝（HTTP " + (int)response.StatusCode + "）。");
                    if (text.Length > 1024 * 1024) throw new AgentFailure("mcp_oauth", "OAuth 令牌响应过大。");
                    Dictionary<string, object> json;
                    try { json = Library.Json.Deserialize<Dictionary<string, object>>(text); } catch { throw new AgentFailure("mcp_oauth", "OAuth 令牌响应无效。"); }
                    if (json == null) throw new AgentFailure("mcp_oauth", "OAuth 令牌响应无效。");
                    string access = Str(json, "access_token");
                    if (access.Length == 0 || access.Length > 8192) throw new AgentFailure("mcp_oauth", "OAuth 授权服务器没有返回访问令牌。");
                    var result = new McpOAuthToken {
                        access_token = access, refresh_token = Str(json, "refresh_token"), token_type = Str(json, "token_type"),
                        scope = Str(json, "scope"), resource = template.resource, client_id = template.client_id, client_secret = template.client_secret,
                        issuer = template.issuer, authorization_endpoint = template.authorization_endpoint, token_endpoint = template.token_endpoint, registration_endpoint = template.registration_endpoint
                    };
                    if (!String.IsNullOrEmpty(Str(json, "expires_in"))) { long seconds; if (Int64.TryParse(Str(json, "expires_in"), out seconds) && seconds > 0 && seconds < 86400 * 365) result.expires_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + seconds; }
                    return result;
                }
            }
        }

        static void RegisterClient(string registrationEndpoint, string redirectUri, out string clientId, out string clientSecret, CancellationToken token) {
            var body = new Dictionary<string, object> {
                { "client_name", "kok" }, { "redirect_uris", new[] { redirectUri } },
                { "grant_types", new[] { "authorization_code", "refresh_token" } }, { "response_types", new[] { "code" } },
                { "token_endpoint_auth_method", "none" }, { "application_type", "native" }
            };
            var request = new HttpRequestMessage(HttpMethod.Post, registrationEndpoint) { Content = new StringContent(Library.Json.Serialize(body), Encoding.UTF8, "application/json") };
            request.Headers.Accept.ParseAdd("application/json");
            HttpResponseMessage response;
            try { response = http.SendAsync(request, token).GetAwaiter().GetResult(); } catch (Exception) { throw new AgentFailure("mcp_oauth", "无法连接 OAuth 客户端注册端点。"); }
            using (response) {
                string text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode) throw new AgentFailure("mcp_oauth", "OAuth 动态客户端注册被拒绝（HTTP " + (int)response.StatusCode + "）。");
                if (text.Length > 1024 * 1024) throw new AgentFailure("mcp_oauth", "OAuth 注册响应过大。");
                Dictionary<string, object> json;
                try { json = Library.Json.Deserialize<Dictionary<string, object>>(text); } catch { throw new AgentFailure("mcp_oauth", "OAuth 注册响应无效。"); }
                clientId = Str(json, "client_id");
                clientSecret = Str(json, "client_secret");
                if (clientId.Length > 500 || clientSecret.Length > 2000) throw new AgentFailure("mcp_oauth", "OAuth 客户端信息无效。");
            }
        }

        static async Task<Dictionary<string, object>> DiscoverProtectedResource(Uri resource, CancellationToken token) {
            string suffix = resource.AbsolutePath == "/" ? "" : resource.AbsolutePath.TrimEnd('/');
            var candidates = new List<string> {
                resource.Scheme + "://" + resource.Authority + "/.well-known/oauth-protected-resource" + suffix,
                resource.Scheme + "://" + resource.Authority + "/.well-known/oauth-protected-resource"
            };
            AgentFailure last = null;
            foreach (var candidate in candidates.Distinct()) {
                try { return await JsonGet(candidate, token); } catch (AgentFailure error) { last = error; }
            }
            throw last ?? new AgentFailure("mcp_oauth", "MCP 服务没有公布 OAuth 受保护资源元数据。");
        }

        static async Task<Dictionary<string, object>> DiscoverAuthorizationServer(string issuer, CancellationToken token) {
            string baseUrl = issuer.TrimEnd('/');
            var candidates = new List<string> {
                baseUrl + "/.well-known/oauth-authorization-server",
                baseUrl + "/.well-known/openid-configuration"
            };
            AgentFailure last = null;
            foreach (var candidate in candidates) {
                try {
                    ValidateEndpoint(candidate, "authorization server metadata");
                    return await JsonGet(candidate, token);
                } catch (AgentFailure error) { last = error; }
            }
            throw last ?? new AgentFailure("mcp_oauth", "无法获取 OAuth 授权服务器元数据。");
        }

        static async Task<Dictionary<string, object>> JsonGet(string url, CancellationToken token) {
            ValidateEndpoint(url, "metadata");
            using (var request = new HttpRequestMessage(HttpMethod.Get, url)) {
                request.Headers.Accept.ParseAdd("application/json");
                HttpResponseMessage response;
                try { response = await http.SendAsync(request, token); } catch (OperationCanceledException) { throw new AgentFailure("mcp_oauth_timeout", "获取 OAuth 元数据超时。"); } catch (HttpRequestException) { throw new AgentFailure("mcp_oauth", "无法连接 OAuth 元数据端点。"); }
                using (response) {
                    if (!response.IsSuccessStatusCode) throw new AgentFailure("mcp_oauth", "OAuth 元数据端点返回 HTTP " + (int)response.StatusCode + "。");
                    string text = await response.Content.ReadAsStringAsync();
                    if (text.Length > 256 * 1024) throw new AgentFailure("mcp_oauth", "OAuth 元数据响应过大。");
                    try { var json = Library.Json.Deserialize<Dictionary<string, object>>(text); if (json == null) throw new InvalidDataException(); return json; } catch { throw new AgentFailure("mcp_oauth", "OAuth 元数据响应无效。"); }
                }
            }
        }

        static void ValidateEndpoint(string value, string field) {
            Uri uri;
            if (String.IsNullOrWhiteSpace(value) || value.Length > 2000 || !Uri.TryCreate(value, UriKind.Absolute, out uri) || !String.IsNullOrEmpty(uri.UserInfo) || !String.IsNullOrEmpty(uri.Fragment) || !(uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback))) throw new AgentFailure("mcp_oauth", "OAuth " + field + " 必须是 HTTPS 地址（本机回环可用 HTTP）。");
        }
        static string Escape(string value) { return Uri.EscapeDataString(value ?? ""); }
        static string Str(Dictionary<string, object> row, string key) { object value; return row != null && row.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : ""; }
        static string FirstString(Dictionary<string, object> row, string key) {
            object value; if (row == null || !row.TryGetValue(key, out value) || value == null) return "";
            var list = Library.Json.Deserialize<List<string>>(Library.Json.Serialize(value));
            return list != null && list.Count > 0 ? list[0] : "";
        }
        static List<string> StrList(Dictionary<string, object> row, string key) {
            object value; if (row == null || !row.TryGetValue(key, out value) || value == null) return new List<string>();
            try { return Library.Json.Deserialize<List<string>>(Library.Json.Serialize(value)) ?? new List<string>(); } catch { return new List<string>(); }
        }
        static Dictionary<string, string> ParseQuery(string query) {
            var result = new Dictionary<string, string>();
            foreach (var part in (query ?? "").TrimStart('?').Split('&')) {
                if (part.Length == 0) continue;
                int at = part.IndexOf('=');
                string key = at >= 0 ? part.Substring(0, at) : part, value = at >= 0 ? part.Substring(at + 1) : "";
                try { result[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value); } catch { }
            }
            return result;
        }
    }
}
