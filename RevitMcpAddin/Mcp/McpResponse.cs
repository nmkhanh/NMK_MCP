using Newtonsoft.Json;

namespace RevitMcpAddin.Mcp
{
    // ═══════════════════════════════════════════════════════════════════════
    //  JSON-RPC 2.0 / MCP response models
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Top-level JSON-RPC 2.0 response.
    /// Exactly one of <see cref="Result"/> or <see cref="Error"/> is non-null.
    /// </summary>
    public class McpResponse
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id")]
        public object? Id { get; set; }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public object? Result { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public McpErrorDetail? Error { get; set; }

        #region Factory helpers

        public static McpResponse Success(object? id, object result) => new()
        {
            Id = id,
            Result = result
        };

        /// <summary>JSON-RPC error code -32601: method not found.</summary>
        public static McpResponse MethodNotFound(object? id, string method) => new()
        {
            Id = id,
            Error = new McpErrorDetail { Code = -32601, Message = $"Method not found: {method}" }
        };

        /// <summary>JSON-RPC error code -32603: internal error.</summary>
        public static McpResponse InternalError(object? id, string message) => new()
        {
            Id = id,
            Error = new McpErrorDetail { Code = -32603, Message = message }
        };

        /// <summary>JSON-RPC error code -32602: invalid params.</summary>
        public static McpResponse InvalidParams(object? id, string message) => new()
        {
            Id = id,
            Error = new McpErrorDetail { Code = -32602, Message = message }
        };

        /// <summary>JSON-RPC error code -32700: parse error.</summary>
        public static McpResponse ParseError() => new()
        {
            Id = null,
            Error = new McpErrorDetail { Code = -32700, Message = "Parse error" }
        };

        #endregion
    }

    /// <summary>JSON-RPC error object.</summary>
    public class McpErrorDetail
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public object? Data { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  MCP tool descriptor (used in tools/list response)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Describes a single MCP tool to the connecting client.</summary>
    public class McpToolDefinition
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// JSON Schema object describing the tool's input.
        /// Use anonymous objects – Newtonsoft.Json will serialise them correctly.
        /// </summary>
        [JsonProperty("inputSchema")]
        public object InputSchema { get; set; } = new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Tool execution result models
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>A single piece of content returned by a tool handler.</summary>
    public class ContentItem
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "text";

        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// The result object that sits inside a <c>tools/call</c> response's
    /// <c>result</c> field.
    /// </summary>
    public class ToolHandlerResult
    {
        [JsonProperty("content")]
        public List<ContentItem> Content { get; set; } = new();

        [JsonProperty("isError")]
        public bool IsError { get; set; }

        // ── Factories ────────────────────────────────────────────────────

        /// <summary>Plain text success result.</summary>
        public static ToolHandlerResult FromText(string text) => new()
        {
            Content = new List<ContentItem> { new() { Text = text } }
        };

        /// <summary>JSON-formatted success result.</summary>
        public static ToolHandlerResult FromJson(object data) => new()
        {
            Content = new List<ContentItem>
            {
                new() { Text = JsonConvert.SerializeObject(data, Formatting.Indented) }
            }
        };

        /// <summary>Error result that the client will surface.</summary>
        public static ToolHandlerResult FromError(string errorMessage) => new()
        {
            IsError = true,
            Content = new List<ContentItem> { new() { Text = errorMessage } }
        };
    }
}
