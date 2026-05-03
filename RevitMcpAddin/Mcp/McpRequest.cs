using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMcpAddin.Mcp
{
    // ═══════════════════════════════════════════════════════════════════════
    //  JSON-RPC 2.0 / MCP request models
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Top-level JSON-RPC 2.0 request.
    /// <para>
    /// MCP uses JSON-RPC 2.0 as its message format. Requests with a non-null
    /// <see cref="Id"/> expect a response; requests without an Id are
    /// "notifications" and must not be replied to.
    /// </para>
    /// </summary>
    public class McpRequest
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        /// <summary>Null for notifications (no response expected).</summary>
        [JsonProperty("id")]
        public object? Id { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; } = string.Empty;

        [JsonProperty("params")]
        public JToken? Params { get; set; }

        /// <summary>True when this message is a notification (no response).</summary>
        [JsonIgnore]
        public bool IsNotification => Id == null;
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Strongly-typed params for specific MCP methods
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>Params carried in a <c>tools/call</c> request.</summary>
    public class ToolCallParams
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("arguments")]
        public JObject? Arguments { get; set; }
    }

    /// <summary>Params carried in an <c>initialize</c> request.</summary>
    public class InitializeParams
    {
        [JsonProperty("protocolVersion")]
        public string ProtocolVersion { get; set; } = string.Empty;

        [JsonProperty("capabilities")]
        public JObject? Capabilities { get; set; }

        [JsonProperty("clientInfo")]
        public McpClientInfo? ClientInfo { get; set; }
    }

    /// <summary>Information about the connecting MCP client.</summary>
    public class McpClientInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;
    }
}
