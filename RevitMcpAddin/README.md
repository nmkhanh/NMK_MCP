# RevitMCP — Revit 2026 Embedded MCP Server

Revit Add-in that acts as a **Model Context Protocol (MCP) server**, allowing
**Claude Code** to query and manipulate the active Revit document in real time.

```
Claude Code (MCP client)
      ↓  JSON-RPC 2.0 over HTTP SSE
RevitMcpAddin (embedded MCP server, port 5000)
      ↓  RevitTask.RunAsync (Revit.Async)
Revit 2026 API (main thread)
```

---

## Architecture

```
RevitMcpAddin/
 ├── App.cs                     IExternalApplication – bootstraps everything
 ├── Command.cs                 Opens the control panel window
 ├── RevitMcpAddin.addin        Revit manifest (copy to Addins folder)
 │
 ├── Ribbon/
 │    └── RibbonCreator.cs      "MCP" tab → "Claude Tools" panel
 │
 ├── Mcp/
 │    ├── McpServer.cs          HttpListener SSE transport (localhost:5000)
 │    ├── McpRouter.cs          JSON-RPC method dispatcher
 │    ├── McpRequest.cs         Request/params models
 │    ├── McpResponse.cs        Response/error/result models
 │    └── Handlers/
 │         ├── IToolHandler.cs
 │         ├── GetDocumentHandler.cs   → get_active_document
 │         ├── GetElementsHandler.cs   → get_elements
 │         └── CreateWallHandler.cs    → create_wall
 │
 ├── Services/
 │    ├── AsyncQueueService.cs   Channel-based Revit API request queue
 │    ├── ExternalEventService.cs Alternative IExternalEventHandler dispatcher
 │    └── RevitService.cs        High-level Revit API wrapper
 │
 ├── Models/
 │    ├── DocumentInfo.cs
 │    ├── ElementInfo.cs
 │    └── WallModels.cs
 │
 ├── ViewModels/
 │    └── MainViewModel.cs       MVVM ViewModel (CommunityToolkit.Mvvm)
 ├── Views/
 │    └── MainView.xaml(.cs)     WPF control panel
 └── Utils/
      └── Logger.cs              Trace-based logger
```

---

## Prerequisites

| Requirement | Version |
|---|---|
| Revit | 2026 |
| .NET Runtime | 8.0 (ships with Revit 2026) |
| Visual Studio | 2022 (v17.8+) |

---

## Build

1. Open `RevitMcpAddin.csproj` in Visual Studio 2022.
2. Restore NuGet packages (automatic on first build):
   - `Revit.Async` 2.0.0
   - `Newtonsoft.Json` 13.0.3
   - `CommunityToolkit.Mvvm` 8.3.2
3. Verify Revit API hint paths in the `.csproj` are correct for your machine:
   ```
   C:\Program Files\Autodesk\Revit 2026\RevitAPI.dll
   C:\Program Files\Autodesk\Revit 2026\RevitAPIUI.dll
   ```
4. Build (`Ctrl+Shift+B`) → DLL lands in `bin\x64\Debug\`.

---

## Installation

1. Copy the build output to a deployment folder, e.g.:
   ```
   C:\ProgramData\Autodesk\Revit\Addins\2026\RevitMcpAddin\
   ```
2. Edit `RevitMcpAddin.addin` so `<Assembly>` points to the full DLL path,
   then copy the `.addin` file to:
   ```
   %APPDATA%\Autodesk\Revit\Addins\2026\RevitMcpAddin.addin
   ```
3. Launch Revit 2026. The MCP server starts automatically on startup.
4. Confirm: open the **MCP** ribbon tab → "Open MCP Panel" → status shows **Running**.

---

## Connecting Claude Code

Add the server to your Claude Code MCP configuration.

### Option A — project-level (recommended)
Create `.claude/mcp.json` in your project root:
```json
{
  "mcpServers": {
    "revit": {
      "url": "http://localhost:5000/sse"
    }
  }
}
```

### Option B — user-level
Edit `~/.claude/mcp.json` (same content as above).

After saving, reload Claude Code. The `revit` MCP server will appear in the
available tools list.

---

## Available MCP Tools

### `get_active_document`
Returns metadata about the currently open Revit document.

**Input:** *(no arguments)*

**Output:**
```json
{
  "title": "MyBuilding",
  "filePath": "C:/Projects/MyBuilding.rvt",
  "isModified": false,
  "isWorkshared": false,
  "activeViewName": "Level 1",
  "activeViewType": "FloorPlan",
  "elementCount": 4231,
  "revitVersion": "Autodesk Revit 2026"
}
```

---

### `get_elements`
Returns elements filtered by Revit category.

**Input:**
```json
{
  "category": "Walls",
  "limit": 20
}
```

`category` must be a `BuiltInCategory` suffix (without `OST_`):
`Walls`, `Doors`, `Windows`, `Floors`, `Columns`, `Stairs`, `Rooms`, etc.

**Output:**
```json
{
  "category": "Walls",
  "returnedCount": 20,
  "elements": [
    {
      "id": "12345",
      "name": "Basic Wall",
      "category": "Walls",
      "levelId": "2001",
      "familyName": "",
      "typeName": "Generic - 200mm",
      "parameters": { "Width": "200 mm", "Unconnected Height": "3000 mm" }
    }
  ]
}
```

---

### `create_wall`
Creates a straight wall in the active document.

**Input (all coordinates in feet):**
```json
{
  "startX": 0,
  "startY": 0,
  "endX": 32.8,
  "endY": 0,
  "height": 9.84,
  "levelName": "Level 1",
  "wallTypeName": "Basic Wall"
}
```

`height` and coordinates are in Revit internal units (**feet**).
- 1 m ≈ 3.281 ft
- 3 m height → `9.84`
- 10 m length → `32.81`

**Output:**
```json
{
  "success": true,
  "elementId": "67890",
  "wallTypeName": "Basic Wall",
  "levelName": "Level 1",
  "message": "Wall created successfully (id=67890)."
}
```

---

## Example Claude Code Session

```
> /mcp
MCP server: revit (http://localhost:5000/sse) ✓

> Tell me about the open Revit document
[calls get_active_document]
The active document is "OfficeBuilding.rvt" (Revit 2026).
It contains 8,423 elements. The active view is "Ground Floor Plan".

> List the first 5 walls
[calls get_elements { "category": "Walls", "limit": 5 }]
Found 5 walls: …

> Create a 10-metre wall on Level 1 from origin
[calls create_wall { "startX":0, "startY":0, "endX":32.81, "endY":0,
                     "height":9.84, "levelName":"Level 1" }]
Wall created (id=123456), type "Basic Wall".
```

---

## Adding New Tools

1. Create `Mcp/Handlers/MyNewHandler.cs` implementing `IToolHandler`.
2. Implement `ToolName`, `GetDefinition()`, and `HandleAsync()`.
3. Add the handler logic in `Services/RevitService.cs`.
4. Register in `App.cs`:
   ```csharp
   McpRouter = new McpRouter()
       .Register(new GetDocumentHandler(RevitSvc))
       .Register(new GetElementsHandler(RevitSvc))
       .Register(new CreateWallHandler(RevitSvc))
       .Register(new MyNewHandler(RevitSvc));  // ← add here
   ```
5. Rebuild and restart Revit. The tool is automatically advertised to Claude.

---

## Threading Model

```
HTTP thread (McpServer)
  ↓ awaits
AsyncQueueService (Channel<QueuedWork>)
  ↓ single background consumer
RevitTask.RunAsync()     ← Revit.Async patches the idle loop
  ↓ scheduled on
Revit Main Thread        ← only thread allowed to call Revit API
  ↑ result flows back via TaskCompletionSource
HTTP thread              ← serialises response to SSE stream
```

**Important:** Never call Revit API directly from handler code. Always go
through `RevitService`, which uses `AsyncQueueService.EnqueueAsync()`.

---

## Configuration

| Setting | Default | Where to change |
|---|---|---|
| HTTP port | 5000 | `McpServer.DefaultPrefix` in `McpServer.cs` |
| Request timeout | 30 s | `AsyncQueueService.DefaultTimeoutMs` |
| Queue depth | 100 | `AsyncQueueService.MaxQueueDepth` |
| Max elements per query | 500 | `GetElementsHandler.MaxLimit` |

---

## Security Note

The server listens on **localhost only** (`http://localhost:5000/`).  
It is not exposed to the network. Any process running on the same machine
can call the API. For shared/multi-user environments consider adding a
Bearer token check in `McpServer.HandleMessagesEndpointAsync`.

---

## License

MIT — see `LICENSE` file.
