# NMK_MCP — Codebase Reference

> Đọc file này thay vì đọc toàn bộ code. Cập nhật mỗi khi thêm tool hoặc đổi kiến trúc.

---

## 1. Tổng quan kiến trúc

```
Claude Desktop / Claude Code
        │
        │  JSON-RPC 2.0 (STDIO)
        ▼
┌──────────────────────┐
│  RevitMcpStdio       │  Node.js — cầu nối STDIO ↔ HTTP/SSE
│  mcpServer.js        │
│  mcpRouter.js        │  Điều phối JSON-RPC, reply initialize/ping/tools/list
│  revitClient.js      │  Kết nối SSE tới Revit addin, gửi POST /messages
│  tools.js            │  Schema fallback khi Revit offline
└────────┬─────────────┘
         │  HTTP/SSE  (localhost:5000)
         │  GET  /sse              → SSE stream (nhận response)
         │  POST /messages?sessionId=…  → gửi JSON-RPC request
         ▼
┌──────────────────────────────────────────────────────┐
│  RevitMcpAddin  (C# .NET, chạy trong Revit process)  │
│                                                      │
│  McpServer  ─→  McpRouter  ─→  IToolHandler          │
│                                      │               │
│                               RevitService           │
│                  (partial class, functions ở subfolder)
│                                      │               │
│                          AsyncQueueService           │
│                       (bounded Channel, max 100)     │
│                                      │               │
│                          RevitTask.RunAsync          │
│                       (Revit.Async library)          │
│                                      │               │
│                          Revit API (main thread)     │
└──────────────────────────────────────────────────────┘
```

**Luồng một request:**
1. Claude → STDIO → `mcpServer.js` → `mcpRouter.js`
2. `mcpRouter` forward POST đến Revit addin (qua `revitClient.js`)
3. `McpServer` nhận POST → `McpRouter.RouteAsync` → gọi `IToolHandler.HandleAsync`
4. Handler gọi `RevitService.XxxAsync` → `AsyncQueueService.EnqueueAsync`
5. `AsyncQueueService` → `RevitTask.RunAsync` → thực thi Revit API trên main thread
6. Kết quả trả về qua SSE stream → `revitClient` resolve promise → STDIO → Claude

---

## 2. Cấu trúc file

```
NMK_MCP/
├── CODEBASE.md                    ← file này
├── SETUP.md                       ← hướng dẫn cài đặt
│
├── RevitMcpAddin/                 ← C# project (Revit addin)
│   ├── App.cs                     ← IExternalApplication, bootstrap toàn bộ
│   ├── Command.cs                 ← IExternalCommand (OpenMcpPanelCommand)
│   ├── RevitMcpAddin.addin        ← manifest, copy vào %APPDATA%\Revit\Addins\2026\
│   ├── mcp.json                   ← cấu hình MCP cho VS Code / Claude Code
│   ├── ngrok_setup.ps1            ← script khởi động ngrok
│   │
│   ├── Mcp/                       ← MCP protocol layer
│   │   ├── McpServer.cs           ← HTTP/SSE server (HttpListener), port 5000
│   │   ├── McpRouter.cs           ← JSON-RPC dispatcher (initialize/tools/list/tools/call)
│   │   ├── McpRequest.cs          ← model JSON-RPC request
│   │   ├── McpResponse.cs         ← model JSON-RPC response + factory helpers
│   │   └── Handlers/
│   │       ├── IToolHandler.cs            ← interface: ToolName, GetDefinition(), HandleAsync()
│   │       ├── GetDocumentHandler.cs      → tool: get_active_document
│   │       ├── GetElementsHandler.cs      → tool: get_elements
│   │       ├── CreateWallHandler.cs       → tool: create_wall
│   │       ├── SelectElementsHandler.cs   → tool: select_elements_by_ids
│   │       ├── PrintSheetHandler.cs       → tool: print_sheet_to_pdf
│   │       └── ExportCadHandler.cs        → tool: export_sheet_to_cad
│   │                                         + tool: list_cad_export_templates
│   │
│   ├── Models/                    ← request/response DTOs
│   │   ├── DocumentInfo.cs
│   │   ├── ElementInfo.cs
│   │   ├── PrintSheetModels.cs    ← PrintSheetRequest { SheetId, OutputFolder, ColorMode, RasterQuality }
│   │   ├── ExportCadModels.cs     ← ExportCadRequest { SheetId, OutputFolder, TemplateName, FileFormat }
│   │   └── WallModels.cs
│   │
│   ├── Services/
│   │   ├── RevitService.cs        ← partial class hub (chỉ khai báo _queue)
│   │   ├── AsyncQueueService.cs   ← bounded Channel(100), timeout, serialize Revit calls
│   │   ├── ExternalEventService.cs
│   │   └── Functions/             ← partial class extensions của RevitService
│   │       ├── F_Document.cs      → GetDocumentInfoAsync
│   │       ├── F_Elements.cs      → GetElementsAsync
│   │       ├── F_Walls.cs         → CreateWallAsync
│   │       ├── F_Selection.cs     → SelectElementsAsync
│   │       ├── F_Print.cs         → PrintSheetToPdfAsync  (in 1 sheet/lần)
│   │       ├── F_CADExport.cs     → ExportSheetToCadAsync + ListCadExportTemplatesAsync
│   │       ├── PaperFormManager.cs← tạo custom Windows paper form (Win32 DevMode)
│   │       └── PDF24Setup.cs      ← cấu hình PDF24 auto-save qua registry
│   │
│   ├── Ribbon/
│   │   └── RibbonCreator.cs       ← tạo tab "MCP" trên Revit ribbon
│   │
│   ├── Utils/
│   │   └── Logger.cs              ← thread-safe, Trace + optional file log
│   │                                 log path: %LocalAppData%\RevitMCP\revitmcp.log
│   │
│   └── Views/ + ViewModels/
│       ├── MainView.xaml          ← WPF control panel
│       └── MainViewModel.cs
│
└── RevitMcpStdio/                 ← Node.js STDIO bridge
    ├── package.json               ← deps: dotenv, eventsource; node ≥ 18
    ├── mcpServer.js               ← entry point, đọc stdin / ghi stdout
    ├── mcpRouter.js               ← dispatch initialize/ping/tools/list/tools/call
    ├── revitClient.js             ← SSE connect + POST /messages + pending map
    └── tools.js                   ← hardcoded schema fallback khi Revit offline
```

---

## 3. Catalog MCP Tools

Tất cả tools đăng ký trong `App.cs → McpRouter.Register(...)`.

### `get_active_document`
- **Mô tả:** metadata document đang mở
- **Input:** (không có)
- **Output:** `{ title, filePath, isModified, isWorkshared, activeViewName, activeViewType, elementCount, revitVersion }`

### `get_elements`
- **Mô tả:** lấy elements theo category, có thể filter theo parameter
- **Input:**
  ```
  category         string   required  (e.g. "Walls", "Sheets", "Doors")
  includeParameters boolean  optional  default false
  parameterFilters  array    optional  [{ name, value }]  AND logic
  ```
- **Output:** `{ elements: [{ id, name, category, parameters? }], ... }`

### `create_wall`
- **Input:**
  ```
  startX, startY, endX, endY  number  required  (feet)
  height                      number  required
  levelName                   string  optional  default "Level 1"
  wallTypeName                string  optional
  ```

### `select_elements_by_ids`
- **Input:**
  ```
  ids            integer[]  required
  zoom           boolean    optional  default true
  clearPrevious  boolean    optional  default true
  ```

### `print_sheet_to_pdf`
- **Mô tả:** in **1 sheet** ra PDF qua PDF24 (bắt buộc cài PDF24)
- **Input:**
  ```
  sheetId       string   required  (Revit element ID, lấy từ get_elements category="Sheets")
  outputFolder  string   optional  default = thư mục của file RVT
  colorMode     string   optional  "Color"(default) | "GrayScale" | "BlackAndWhite"
  rasterQuality string   optional  "Draft"|"Low"|"Medium"|"High"(default)|"Presentation"
  ```
- **Output:**
  ```json
  {
    "success": true,
    "sheet": { "number": "A-001", "name": "Floor Plan", "widthMm": 420.0, "heightMm": 297.0, "paperSize": "A3", "file": "C:\\...\\A-001.pdf" },
    "outputFile": "C:\\...\\A-001.pdf",
    "outputFolder": "C:\\...",
    "printer": "PDF24",
    "message": "Printed sheet 'A-001' to 'PDF24'."
  }
  ```
- **Cơ chế:** PDF24Setup ghi registry auto-save → PrintManager + Transaction → TransactionGroup.Assimilate
- **Timeout:** 180s

### `export_sheet_to_cad`
- **Mô tả:** export **1 sheet** ra DWG hoặc DXF
- **Input:**
  ```
  sheetId       string   required
  outputFolder  string   optional
  templateName  string   optional  (tên ExportDWGSettings trong file RVT)
  fileFormat    string   optional  "DWG"(default) | "DXF"
  ```
- **Output:**
  ```json
  {
    "success": true,
    "exported": true,
    "fileFormat": "DWG",
    "template": "My Template",
    "sheet": { "number": "A-001", "name": "...", "file": "C:\\...\\A-001.dwg", "exported": true },
    "outputFile": "C:\\...\\A-001.dwg",
    "outputFolder": "C:\\...",
    "message": "Exported sheet 'A-001' to DWG using template 'My Template'."
  }
  ```
- **Timeout:** 180s

### `list_cad_export_templates`
- **Mô tả:** liệt kê tên các ExportDWGSettings có trong file RVT
- **Input:** (không có)
- **Output:** `{ templates: ["Template A", "Template B"], templateCount: 2 }`

---

## 4. Các pattern quan trọng

### 4.1 Thêm tool mới (checklist)

1. **Model** — tạo `Models/XxxModels.cs`: request DTO với `[JsonProperty]`
2. **Service** — tạo `Services/Functions/F_Xxx.cs`: `partial class RevitService`, method `XxxAsync` dùng `_queue.EnqueueAsync`
3. **Handler** — tạo `Mcp/Handlers/XxxHandler.cs`: implement `IToolHandler`  
   - `ToolName` → tên tool (snake_case)  
   - `GetDefinition()` → JSON Schema input  
   - `HandleAsync()` → parse args → gọi service
4. **Register** — trong `App.cs`: `.Register(new XxxHandler(RevitSvc))`
5. **Stdio fallback** — thêm vào `RevitMcpStdio/tools.js` phần cuối mảng `TOOLS`

### 4.2 AsyncQueueService

```csharp
// Pattern chuẩn cho mọi RevitService method:
public Task<object> XxxAsync(XxxRequest request, CancellationToken ct = default)
{
  return _queue.EnqueueAsync(async uiApp =>
  {
    var doc = uiApp.ActiveUIDocument?.Document
              ?? throw new InvalidOperationException("No active document.");
    // ... Revit API calls ...
    return (object)new { success = true, ... };
  }, ct, timeoutMs: 30_000);
}
```

- **Max queue depth:** 100 requests
- **Default timeout:** 30s
- **Print/CAD timeout:** 180s

### 4.3 Transaction pattern (F_Print)

```
TransactionGroup.Start()
  Transaction "Configure Print" → SelectNewPrintDriver, PrintSetup, Apply, Print
  Transaction "Cleanup Print Setting" → xóa named print setting tạm
TransactionGroup.Assimilate()
```

Chú ý: `doc.Print(viewSet, true)` gọi BÊN TRONG transaction, `SubmitPrint` gọi bên ngoài.

### 4.4 PDF24 auto-save

`PDF24Setup.SetAutoSave(outputFolder, fileName)` ghi registry:
- `HKCU\Software\PDF24\PDF24\Print\AutoSave = 1`
- Đặt output path để PDF24 tự lưu không hiện dialog

### 4.5 Paper form (PaperFormManager)

`CreatePaperFormMM(formName, widthMm, heightMm)` tạo custom Windows paper form qua `AddForm` (Win32 DevMode). Tên format: `RMCP_{w}x{h}` (ví dụ `RMCP_420x297`).

`FindBestPaperSize` — ưu tiên:
1. Win32 `DeviceCapabilities` (DC_PAPERSIZE + DC_PAPERNAMES) — match dimensions
2. Exact name match
3. Custom RMCP form
4. Partial name
5. Nearest standard size (A0–A4, Letter, Tabloid)

---

## 5. Cấu hình & triển khai

### Revit Addin
```
# Copy file manifest:
%APPDATA%\Autodesk\Revit\Addins\2026\RevitMcpAddin.addin

# DLL build output:
RevitMcpAddin\bin\Debug\RevitMcpAddin.dll
```

### STDIO bridge (Claude Desktop)
```json
// claude_desktop_config.json
{
  "mcpServers": {
    "revit": {
      "command": "node",
      "args": ["D:/Test/RevitMcpStdio/mcpServer.js"],
      "env": { "REVIT_BASE_URL": "http://localhost:5000" }
    }
  }
}
```

### VS Code / Claude Code
```json
// mcp.json (đã có trong repo)
{
  "servers": {
    "revit": {
      "command": "C:/Program Files/nodejs/node.exe",
      "args": ["D:/Test/RevitMcpStdio/mcpServer.js"],
      "env": { "REVIT_BASE_URL": "http://localhost:5000" }
    }
  }
}
```

### ngrok (access từ Claude.ai web)
```powershell
.\ngrok_setup.ps1   # hoặc: ngrok http 5000
# Thêm https://xxxx.ngrok-free.app/sse vào Claude → Settings → Integrations
```

### URL ACL (nếu dùng wildcard `http://+:5000/`)
```powershell
# Chạy 1 lần với quyền admin:
netsh http add urlacl url=http://+:5000/ user=Everyone
```

### Env vars (RevitMcpStdio)
| Biến | Mặc định | Mô tả |
|---|---|---|
| `REVIT_BASE_URL` | `http://localhost:5000` | URL của Revit addin |
| `REQUEST_TIMEOUT_MS` | `120000` | timeout per request (ms) |
| `DEBUG` | (unset) | verbose logging ra stderr |

---

## 6. HTTP endpoints (McpServer)

| Method | Path | Mô tả |
|---|---|---|
| GET | `/sse` | mở SSE stream, nhận `event: endpoint` với POST URL |
| POST | `/messages?sessionId=<id>` | gửi JSON-RPC request, nhận 202 ngay |
| GET | `/health` | health check (trả `{"status":"ok"}`) |

**SSE heartbeat:** comment `: keep-alive` mỗi 5 giây.

---

## 7. JSON-RPC methods (McpRouter)

| Method | Xử lý |
|---|---|
| `initialize` | trả server info + capabilities (tools: {}) |
| `notifications/initialized` | notification, không reply |
| `tools/list` | trả danh sách tool definitions từ tất cả handlers |
| `tools/call` | gọi handler tương ứng theo `params.name` |
| `ping` | trả `{}` |
| Khác | error -32601 Method Not Found |

**Protocol version:** `2024-11-05`

---

## 8. Log

```
%LocalAppData%\RevitMCP\revitmcp.log   ← addin log
stderr của mcpServer.js                ← stdio bridge log (DEBUG=1 để verbose)
```

---

## 9. Dependencies

### C# (RevitMcpAddin.csproj)
- Autodesk Revit API 2026
- Revit.Async (RevitTask)
- Newtonsoft.Json

### Node.js (package.json)
- dotenv ^16.4.5
- eventsource ^2.0.2
- Node ≥ 18
