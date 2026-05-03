using Newtonsoft.Json.Linq;
using RevitMcpAddin.Mcp;
using RevitMcpAddin.Models;
using RevitMcpAddin.Services;
using RevitMcpAddin.Utils;

namespace RevitMcpAddin.Mcp.Handlers
{
    /// <summary>
    /// MCP tool: <b>create_wall</b>
    /// Creates a straight wall in the active Revit document.
    ///
    /// All coordinates are in Revit internal units (decimal feet).
    /// 1 foot ≈ 0.3048 m.  Use UnitUtils to convert if needed.
    ///
    /// Input:
    /// <code>
    /// {
    ///   "startX":      0.0,        // required
    ///   "startY":      0.0,        // required
    ///   "endX":       10.0,        // required
    ///   "endY":        0.0,        // required
    ///   "height":      9.84,       // required (approx 3 m)
    ///   "levelName":  "Level 1",   // optional
    ///   "wallTypeName": "Basic Wall - Generic 200mm" // optional
    /// }
    /// </code>
    /// </summary>
    public sealed class CreateWallHandler : IToolHandler
    {
        #region Fields

        private readonly RevitService _revitService;

        #endregion

        #region Constructor

        public CreateWallHandler(RevitService revitService)
        {
            _revitService = revitService;
        }

        #endregion

        #region IToolHandler

        public string ToolName => "create_wall";

        public McpToolDefinition GetDefinition() => new()
        {
            Name        = ToolName,
            Description = "Creates a straight wall in the active Revit document. " +
                          "Coordinates are in Revit internal units (feet). " +
                          "Returns the new element's Id and type.",
            InputSchema = new
            {
                type       = "object",
                properties = new
                {
                    startX = new
                    {
                        type        = "number",
                        description = "Wall start X coordinate (feet)."
                    },
                    startY = new
                    {
                        type        = "number",
                        description = "Wall start Y coordinate (feet)."
                    },
                    endX = new
                    {
                        type        = "number",
                        description = "Wall end X coordinate (feet)."
                    },
                    endY = new
                    {
                        type        = "number",
                        description = "Wall end Y coordinate (feet)."
                    },
                    height = new
                    {
                        type        = "number",
                        description = "Wall height in feet (e.g. 9.84 ≈ 3 m).",
                        minimum     = 0.1
                    },
                    levelName = new
                    {
                        type        = "string",
                        description = "Name of the level to place the wall on (e.g. 'Level 1'). " +
                                      "Uses the first available level if omitted.",
                        @default    = "Level 1"
                    },
                    wallTypeName = new
                    {
                        type        = "string",
                        description = "Name of the wall type (e.g. 'Basic Wall'). " +
                                      "Uses the project default if omitted."
                    }
                },
                required = new[] { "startX", "startY", "endX", "endY", "height" }
            }
        };

        public async Task<ToolHandlerResult> HandleAsync(
            JObject? arguments,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // ── Parse and validate arguments ─────────────────────────
                var request = ParseRequest(arguments);

                Logger.Info($"create_wall: ({request.StartX},{request.StartY})→" +
                            $"({request.EndX},{request.EndY}), h={request.Height}, " +
                            $"level='{request.LevelName}'");

                // ── Delegate to RevitService (creates transaction internally) ──
                var result = await _revitService.CreateWallAsync(request, cancellationToken);

                return ToolHandlerResult.FromJson(result);
            }
            catch (ArgumentException ex)
            {
                Logger.Warning($"CreateWallHandler invalid argument: {ex.Message}");
                return ToolHandlerResult.FromError(ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error("CreateWallHandler error", ex);
                return ToolHandlerResult.FromError($"Failed to create wall: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private static CreateWallRequest ParseRequest(JObject? args)
        {
            if (args == null)
                throw new ArgumentException("Arguments are required for create_wall.");

            double Req(string key)
            {
                var token = args[key];
                if (token == null)
                    throw new ArgumentException($"'{key}' is required.");
                return token.Value<double>();
            }

            var height = Req("height");
            if (height <= 0)
                throw new ArgumentException("'height' must be a positive number.");

            return new CreateWallRequest
            {
                StartX       = Req("startX"),
                StartY       = Req("startY"),
                EndX         = Req("endX"),
                EndY         = Req("endY"),
                Height       = height,
                LevelName    = args["levelName"]?.Value<string>()    ?? "Level 1",
                WallTypeName = args["wallTypeName"]?.Value<string>()
            };
        }

        #endregion
    }
}
