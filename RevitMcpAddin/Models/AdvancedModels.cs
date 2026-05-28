using Newtonsoft.Json;

namespace RevitMcpAddin.Models
{
    public sealed class ActivateFamilySymbolRequest
    {
        [JsonProperty("symbolId")]
        public string? SymbolId { get; set; }
    }
}
