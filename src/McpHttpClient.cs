using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace kok {
    // Streamable HTTP transport for remote MCP servers. The JSON-RPC handshake, schema validation,
    // approvals and bounded results stay in the shared MCP layer; only the wire transport differs.
    public sealed class McpHttpClient : IMcpClient {
        readonly Uri endpoint; readonly string token; readonly HttpClient client; readonly object gate = new object();
        int serial; bool closed; string sessionId; public string Protocol { get; private set; }
        public McpHttpClient(Uri endpoint, string token = "") {
            this.endpoint = ValidateEndpoint(endpoint == null ? "" : endpoint.AbsoluteUri);
            this.token = token ?? "";
            if (this.token.Length > 4096 || this.token.Any(Char.IsControl)) throw new AgentFailure("mcp_config", "MCP 访问令牌格式无效。");
            var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = !this.endpoint.IsLoopback };
            client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };
        }
        public static Uri ValidateEndpoint(string value) {
            Uri uri;
            if (String.IsNullOrWhiteSpace(value) || value.Length > 2000 || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri) || !String.IsNullOrEmpty(uri.UserInfo) || !String.IsNullOrEmpty(uri.Query) || !String.IsNullOrEmpty(uri.Fragment) || !(uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback)) || uri.AbsolutePath.Length > 500) throw new AgentFailure("mcp_config", "远程 MCP 需要 HTTPS 地址；本机服务可使用 http://localhost 或 127.0.0.1。");
            return uri;
        }
        public bool Connected { get { lock (gate) return !closed; } }
        public bool CatalogChanged { get; set; }

        public async Task Initialize(CancellationToken token) {
            bool legacy = false;
            try {
                var discovery = await Request("server/discover", new Dictionary<string,object>(), token, 3000, true, false);
                if (discovery == null || !discovery.ContainsKey("supportedVersions") || !Library.Json.Deserialize<List<string>>(Library.Json.Serialize(discovery["supportedVersions"])).Contains(McpStdioClient.ModernVersion)) legacy = true;
                else Protocol = McpStdioClient.ModernVersion;
            } catch (McpRpcException error) {
                if (error.code <= -32020 && error.code >= -32099) { if (error.code == -32022 && error.data != null && error.data.ContainsKey("supported") && Library.Json.Deserialize<List<string>>(Library.Json.Serialize(error.data["supported"])).Contains(McpStdioClient.ModernVersion)) Protocol = McpStdioClient.ModernVersion; else throw new AgentFailure("mcp_version", "MCP 协议版本不兼容。"); }
                else legacy = true;
            } catch (TimeoutException) { legacy = true; } catch (HttpRequestException) { throw new AgentFailure("mcp_connect", "无法连接远程 MCP 服务。"); } catch (TaskCanceledException) { throw new AgentFailure("mcp_timeout", "连接远程 MCP 服务超时。"); }
            if (Protocol == McpStdioClient.ModernVersion) { await SendNotification("notifications/initialized", new Dictionary<string,object>(), token); return; }
            if (!legacy) throw new AgentFailure("mcp_version", "MCP 协议版本不兼容。");
            Protocol = "2025-11-25";
            var result = await Request("initialize", new Dictionary<string,object> { { "protocolVersion", Protocol }, { "capabilities", new Dictionary<string,object>() }, { "clientInfo", new { name = "kok", version = AppVersion.Current } } }, token, 15000, false);
            string version = result != null && result.ContainsKey("protocolVersion") ? Convert.ToString(result["protocolVersion"]) : "";
            if (!new[] { "2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05" }.Contains(version)) throw new AgentFailure("mcp_version", "MCP 协议版本不兼容。");
            Protocol = version;
            await SendNotification("notifications/initialized", new Dictionary<string,object>(), token);
        }

        public async Task<Dictionary<string,object>> Request(string method, Dictionary<string,object> args, CancellationToken token, int timeoutMs = 15000, bool metadata = true, bool closeOnTimeout = true) {
            token.ThrowIfCancellationRequested();
            string id = Interlocked.Increment(ref serial).ToString();
            var parameters = new Dictionary<string,object>(args ?? new Dictionary<string,object>());
            if (metadata && (Protocol == null || Protocol == McpStdioClient.ModernVersion)) parameters["_meta"] = new Dictionary<string,object> { { "io.modelcontextprotocol/protocolVersion", McpStdioClient.ModernVersion }, { "io.modelcontextprotocol/clientInfo", new { name = "kok", version = AppVersion.Current } }, { "io.modelcontextprotocol/clientCapabilities", new Dictionary<string,object>() } };
            var body = new { jsonrpc = "2.0", id, method, @params = parameters };
            var messages = await PostAsync(body, token, timeoutMs, true);
            foreach (var message in messages) {
                object value;
                if (message.TryGetValue("method", out value)) { if (Convert.ToString(value) == "notifications/tools/list_changed") CatalogChanged = true; continue; }
                if (!message.TryGetValue("id", out value) || Convert.ToString(value) != id) continue;
                return Result(message);
            }
            throw new AgentFailure("mcp_uncertain", "远程 MCP 未返回匹配的响应，结果未知，不能自动重放。请检查服务后重新连接。");
        }

        async Task SendNotification(string method, Dictionary<string,object> args, CancellationToken token) {
            var body = new { jsonrpc = "2.0", method, @params = args ?? new Dictionary<string,object>() };
            await PostAsync(body, token, 15000, false);
        }

        static Dictionary<string,object> Result(Dictionary<string,object> message) {
            object value;
            if (message.TryGetValue("error", out value)) { var error = value as Dictionary<string,object>; int code = error != null && error.ContainsKey("code") ? Convert.ToInt32(error["code"]) : -32603; throw new McpRpcException(code, error != null && error.ContainsKey("data") ? error["data"] as Dictionary<string,object> : null); }
            var result = message.ContainsKey("result") ? message["result"] as Dictionary<string,object> : null;
            if (result == null) throw new InvalidDataException();
            if (result.ContainsKey("resultType") && Convert.ToString(result["resultType"]) != "complete") throw new AgentFailure("mcp_interaction", "外部工具需要额外交互，当前不会自动授权或继续。");
            return result;
        }

        async Task<List<Dictionary<string,object>>> PostAsync(object body, CancellationToken token, int timeoutMs, bool expectResponse) {
            token.ThrowIfCancellationRequested();
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(500, timeoutMs)));
                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint)) {
                    request.Headers.Accept.ParseAdd("application/json, text/event-stream");
                    request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", Protocol ?? McpStdioClient.ModernVersion);
                    string currentSession; lock (gate) currentSession = sessionId;
                    if (!String.IsNullOrEmpty(currentSession)) request.Headers.TryAddWithoutValidation("Mcp-Session-Id", currentSession);
                    if (!String.IsNullOrEmpty(tokenValue())) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenValue());
                    request.Content = new StringContent(Library.Json.Serialize(body), Encoding.UTF8, "application/json");
                    HttpResponseMessage response;
                    try { response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token); }
                    catch (OperationCanceledException) { if (token.IsCancellationRequested) throw; throw new AgentFailure("mcp_timeout", "远程 MCP 请求超时，结果未知，不能自动重放。"); }
                    catch (HttpRequestException error) { throw new AgentFailure("mcp_connect", "无法连接远程 MCP 服务：" + error.Message); }
                    using (response) {
                        string session = ResponseHeader(response, "Mcp-Session-Id");
                        if (!String.IsNullOrEmpty(session)) lock (gate) sessionId = session;
                        int status = (int)response.StatusCode;
                        if (status == 401 || status == 403) throw new AgentFailure("mcp_auth", "远程 MCP 拒绝了访问令牌。");
                        if (status == 202 || status == 204) return new List<Dictionary<string,object>>();
                        if (!response.IsSuccessStatusCode) throw new AgentFailure("mcp_http", "远程 MCP 返回 HTTP " + status + "。");
                        string media = response.Content.Headers.ContentType != null ? (response.Content.Headers.ContentType.MediaType ?? "") : "";
                        if (!expectResponse) return new List<Dictionary<string,object>>();
                        if (media.IndexOf("text/event-stream", StringComparison.OrdinalIgnoreCase) >= 0) return await ReadEventStream(response, timeout.Token);
                        string text;
                        using (var stream = await response.Content.ReadAsStreamAsync()) using (var memory = new MemoryStream()) {
                            var buffer = new byte[8192]; int read;
                            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token)) > 0) { if (memory.Length + read > 1024 * 1024) throw new AgentFailure("mcp_schema", "远程 MCP 响应过大。"); memory.Write(buffer, 0, read); }
                            text = Encoding.UTF8.GetString(memory.ToArray());
                        }
                        if (String.IsNullOrWhiteSpace(text)) throw new InvalidDataException();
                        var message = Library.Json.Deserialize<Dictionary<string,object>>(text);
                        if (message == null) throw new InvalidDataException();
                        return new List<Dictionary<string,object>> { message };
                    }
                }
            }
        }

        string tokenValue() { return token; }
        static string ResponseHeader(HttpResponseMessage response, string name) {
            IEnumerable<string> values; return response.Headers.TryGetValues(name, out values) ? values.FirstOrDefault() : "";
        }

        async Task<List<Dictionary<string,object>>> ReadEventStream(HttpResponseMessage response, CancellationToken token) {
            var messages = new List<Dictionary<string,object>>();
            var data = new StringBuilder();
            using (var stream = await response.Content.ReadAsStreamAsync()) using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                int total = 0; string line;
                while ((line = await reader.ReadLineAsync()) != null) {
                    total += line.Length + 1; if (total > 1024 * 1024) throw new AgentFailure("mcp_schema", "远程 MCP 响应过大。");
                    token.ThrowIfCancellationRequested();
                    if (line.Length == 0) { if (data.Length > 0) { var parsed = Library.Json.Deserialize<Dictionary<string,object>>(data.ToString()); if (parsed != null) messages.Add(parsed); data.Clear(); } continue; }
                    if (line.StartsWith("data:", StringComparison.Ordinal)) data.Append(line.Substring(5).TrimStart());
                }
                if (data.Length > 0) { var parsed = Library.Json.Deserialize<Dictionary<string,object>>(data.ToString()); if (parsed != null) messages.Add(parsed); }
            }
            if (messages.Count == 0) throw new AgentFailure("mcp_uncertain", "远程 MCP 事件流未返回结果。");
            return messages;
        }

        public void Dispose() { lock (gate) { if (closed) return; closed = true; } client.Dispose(); }
    }
}
