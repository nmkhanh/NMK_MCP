# RevitMCP — Hướng dẫn cài đặt và chạy hệ thống

## Kiến trúc tổng quan

```
Claude Desktop
    │  STDIO (stdin/stdout JSON-RPC)
    ▼
RevitMcpStdio/mcpServer.js          ← Node.js process (được spawn bởi Claude Desktop)
    │  HTTP SSE  (localhost:5000 hoặc ngrok URL)
    ▼
Revit 2026 + RevitMcpAddin          ← C# Add-in chạy bên trong Revit
```

---

## Phần 1 — Chuẩn bị (một lần duy nhất)

### 1.1 Cài đặt Node.js (≥ 18)

Tải tại: https://nodejs.org/  
Kiểm tra: `node --version`

### 1.2 Cài npm packages cho STDIO server

```powershell
cd D:\Test\RevitMcpStdio
npm install
```

### 1.3 Build Revit Add-in

Mở `D:\Test\RevitMcpAddin\RevitMcpAddin.sln` trong Visual Studio 2022.  
Build → Release x64.

Copy file `.addin` + DLL vào thư mục Revit addins:
```
%APPDATA%\Autodesk\Revit\Addins\2026\
```
File cần copy:
- `RevitMcpAddin.addin`
- `bin\Release\net8.0-windows\RevitMcpAddin.dll`
- Tất cả DLL phụ thuộc trong cùng thư mục bin

### 1.4 Cấu hình Claude Desktop

Mở Claude Desktop → **Settings → Developer → Edit Config**

Dán nội dung sau (thay đường dẫn nếu cần):

```json
{
  "mcpServers": {
    "revit": {
      "command": "C:/Program Files/nodejs/node.exe",
      "args": ["D:/Test/RevitMcpStdio/mcpServer.js"],
      "env": {
        "REVIT_BASE_URL": "http://localhost:5000"
      }
    }
  }
}
```

Lưu file, **tắt hoàn toàn** (system tray → Quit) và mở lại Claude Desktop.

---

## Phần 2 — Chạy cục bộ (local, một máy)

### Bước 1: Mở Revit 2026

Revit sẽ tự động load add-in. Kiểm tra ribbon "RevitMCP" xuất hiện.

### Bước 2: Khởi động MCP Server trong Revit

Trên ribbon **RevitMCP** → click **"Start Server"**.

Kiểm tra server hoạt động:
```powershell
Invoke-RestMethod http://localhost:5000/health
```
Kết quả mong đợi: `{ status: "ok", server: "RevitMCP" }`

### Bước 3: Mở Claude Desktop

Góc dưới bên phải chat box sẽ xuất hiện icon 🔨 (tools).  
Click vào → chọn **revit** → xác nhận các tools sẵn sàng.

### Bước 4: Thử dùng

Gõ vào Claude:
- *"Lấy thông tin tài liệu Revit đang mở"*
- *"Liệt kê tất cả tường trong dự án"*
- *"Chọn element id 123456"*

---

## Phần 3 — Public Access (nhiều máy / internet)

### Phương án A: LAN (cùng mạng nội bộ)

#### Bước A1: Cấp URL ACL cho wildcard binding (chạy một lần, với quyền Admin)

```powershell
netsh http add urlacl url=http://+:5000/ user=Everyone
```

> Add-in đã tự cố bind `http://+:5000/` trước, tự fallback về `localhost:5000` nếu chưa có ACL.  
> Sau khi chạy lệnh trên, khởi động lại Revit để bind wildcard thành công.

#### Bước A2: Mở firewall port 5000

```powershell
New-NetFirewallRule -DisplayName "RevitMCP" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow
```

#### Bước A3: Cấu hình Claude Desktop trên máy khách

Lấy IP của máy chạy Revit (ví dụ `192.168.1.100`):

```json
{
  "mcpServers": {
    "revit": {
      "command": "C:/Program Files/nodejs/node.exe",
      "args": ["D:/Test/RevitMcpStdio/mcpServer.js"],
      "env": {
        "REVIT_BASE_URL": "http://192.168.1.100:5000"
      }
    }
  }
}
```

---

### Phương án B: Internet (qua ngrok)

#### Bước B1: Cài ngrok và đăng ký token

```powershell
# Chạy script có sẵn
D:\Test\RevitMcpAddin\ngrok_setup.ps1
```

Hoặc thủ công:
1. Tải ngrok: https://ngrok.com/download
2. Đăng ký tài khoản → lấy authtoken
3. `ngrok config add-authtoken <TOKEN>`

#### Bước B2: Expose Revit MCP qua ngrok

```powershell
ngrok http 5000
```

Lấy HTTPS URL từ output, ví dụ: `https://abc123.ngrok-free.app`

#### Bước B3: Cấu hình Claude Desktop (máy khách)

```json
{
  "mcpServers": {
    "revit": {
      "command": "C:/Program Files/nodejs/node.exe",
      "args": ["D:/Test/RevitMcpStdio/mcpServer.js"],
      "env": {
        "REVIT_BASE_URL": "https://abc123.ngrok-free.app"
      }
    }
  }
}
```

> **Lưu ý**: URL ngrok miễn phí thay đổi mỗi lần restart ngrok.  
> Dùng tên miền cố định: `ngrok http 5000 --domain=your-name.ngrok-free.app` (cần đăng ký trong dashboard ngrok).

---

## Phần 4 — Danh sách Tools MCP

| Tool | Mô tả | Tham số bắt buộc |
|------|--------|-----------------|
| `get_active_document` | Thông tin tài liệu đang mở | _(không có)_ |
| `get_elements` | Lấy danh sách elements theo category | `category` |
| `create_wall` | Tạo tường thẳng | `startX, startY, endX, endY, height` |
| `select_elements_by_ids` | Chọn & zoom đến elements | `ids` |

### get_elements — tham số đầy đủ

| Tham số | Kiểu | Mặc định | Mô tả |
|---------|------|----------|-------|
| `category` | string | _(bắt buộc)_ | Ví dụ: `"Walls"`, `"Doors"`, `"Floors"` |
| `limit` | int | `50` | Số lượng tối đa. `0` = tất cả (tối đa 2000) |
| `includeParameters` | bool | `false` | Bật để lấy thêm parameters (chậm hơn) |

### select_elements_by_ids — tham số đầy đủ

| Tham số | Kiểu | Mặc định | Mô tả |
|---------|------|----------|-------|
| `ids` | int[] | _(bắt buộc)_ | Danh sách ElementId |
| `zoom` | bool | `true` | Zoom view đến selection |
| `clearPrevious` | bool | `true` | Xóa selection cũ trước khi chọn mới |

---

## Phần 5 — Khắc phục sự cố

### Server không start

```powershell
# Kiểm tra port 5000 có bị dùng không
netstat -ano | findstr :5000

# Kiểm tra health
Invoke-RestMethod http://localhost:5000/health
```

### Claude Desktop không thấy tools

1. Kiểm tra config đúng file: **Settings → Developer → Edit Config** (không phải `%APPDATA%\Claude\`)
2. Tắt hoàn toàn Claude Desktop (system tray → Quit) và mở lại
3. Test STDIO server thủ công:
   ```powershell
   echo '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | node D:\Test\RevitMcpStdio\mcpServer.js 2>$null
   ```

### get_elements bị timeout

- Dùng `limit` nhỏ hơn (ví dụ `50`)
- Không bật `includeParameters: true` với nhiều elements
- Kiểm tra Revit có đang busy không (đang tải file, đang render...)

### ngrok báo lỗi

```powershell
# Kiểm tra ngrok đã được auth chưa
ngrok config check

# Xem log ngrok
ngrok http 5000 --log=stdout
```

---

## Phần 6 — Cấu trúc thư mục

```
D:\Test\
├── RevitMcpAddin\          ← C# Revit Add-in (Visual Studio project)
│   ├── App.cs              ← Entry point, khởi tạo tất cả services
│   ├── Mcp\
│   │   ├── McpServer.cs    ← HTTP/SSE server (port 5000)
│   │   ├── McpRouter.cs    ← JSON-RPC dispatcher
│   │   └── Handlers\       ← Tool handlers (1 file / 1 tool)
│   ├── Services\
│   │   ├── RevitService.cs       ← Revit API wrapper
│   │   └── AsyncQueueService.cs  ← Thread-safe Revit task queue
│   └── ngrok_setup.ps1     ← Script cài và chạy ngrok
│
└── RevitMcpStdio\          ← Node.js STDIO MCP server (Claude Desktop)
    ├── mcpServer.js         ← Entry point (stdin/stdout)
    ├── mcpRouter.js         ← JSON-RPC dispatch
    ├── revitClient.js       ← SSE client kết nối tới Revit add-in
    └── tools.js             ← Fallback tool definitions
```
