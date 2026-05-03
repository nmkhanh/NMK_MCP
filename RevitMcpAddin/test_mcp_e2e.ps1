# ============================================================
#  test_mcp_e2e.ps1
#  End-to-end smoke test for RevitMCP server
#  Run BEFORE connecting Claude to verify the server is healthy.
# ============================================================

param(
    [string]$BaseUrl = "http://localhost:5000"
)

$pass = 0; $fail = 0

function Test-Check([string]$name, [scriptblock]$block) {
    try {
        $result = & $block
        if ($result) {
            Write-Host "  [PASS] $name" -ForegroundColor Green
            $script:pass++
        } else {
            Write-Host "  [FAIL] $name" -ForegroundColor Red
            $script:fail++
        }
    } catch {
        Write-Host "  [FAIL] $name — $_" -ForegroundColor Red
        $script:fail++
    }
}

Write-Host "`nRevitMCP End-to-End Test  ($BaseUrl)" -ForegroundColor Cyan
Write-Host "=" * 50

# ── 1. Health check ────────────────────────────────────────────────────────
Write-Host "`n[1] Health / Discovery"
Test-Check "GET /health returns 200" {
    $r = Invoke-WebRequest "$BaseUrl/health" -UseBasicParsing -EA Stop
    $r.StatusCode -eq 200
}
Test-Check "GET /health returns JSON with status=ok" {
    $r = Invoke-WebRequest "$BaseUrl/health" -UseBasicParsing -EA Stop
    $body = $r.Content | ConvertFrom-Json
    $body.status -eq "ok"
}

# ── 2. SSE connection ──────────────────────────────────────────────────────
Write-Host "`n[2] SSE endpoint  GET /sse"

$sseJob = Start-Job {
    param($url)
    try {
        $req = [System.Net.HttpWebRequest]::Create("$url/sse")
        $req.Accept  = "text/event-stream"
        $req.Timeout = 8000
        $resp   = $req.GetResponse()
        $stream = $resp.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)

        $lines = @()
        $deadline = [DateTime]::UtcNow.AddSeconds(6)
        while ([DateTime]::UtcNow -lt $deadline) {
            if ($reader.Peek() -ge 0) {
                $lines += $reader.ReadLine()
                if ($lines.Count -ge 4) { break }
            } else {
                Start-Sleep -Milliseconds 100
            }
        }
        $lines | ConvertTo-Json
    } catch { "ERROR: $_" }
} -ArgumentList $BaseUrl

$null = Wait-Job $sseJob -Timeout 10
$sseOutput = Receive-Job $sseJob
Remove-Job $sseJob -Force

Test-Check "SSE endpoint responds" {
    $sseOutput -ne $null -and $sseOutput -notlike "ERROR:*"
}

$sseLines = $sseOutput | ConvertFrom-Json -ErrorAction SilentlyContinue
Test-Check "SSE sends 'event: endpoint' line" {
    $sseLines -contains "event: endpoint"
}
Test-Check "SSE endpoint event contains POST URL" {
    ($sseLines | Where-Object { $_ -like "data: *messages*sessionId*" }).Count -gt 0
}

# ── 3. Extract session ID from SSE output ─────────────────────────────────
$endpointLine = $sseLines | Where-Object { $_ -like "data: *messages*sessionId*" }
$sessionId = ""
if ($endpointLine) {
    if ($endpointLine -match 'sessionId=([a-f0-9]+)') {
        $sessionId = $Matches[1]
    }
}

Write-Host "`n[3] JSON-RPC over POST /messages  (sessionId=$($sessionId[0..7] -join '')…)"

function Send-JsonRpc([string]$method, [hashtable]$params = @{}, [string]$id = "1") {
    $body = @{ jsonrpc = "2.0"; id = $id; method = $method; params = $params } | ConvertTo-Json -Depth 5
    $url  = "$BaseUrl/messages?sessionId=$sessionId"
    $r    = Invoke-WebRequest $url -Method POST `
               -ContentType "application/json" `
               -Body $body `
               -UseBasicParsing -EA Stop
    $r.StatusCode
}

Test-Check "POST /messages returns 202 for 'initialize'" {
    $status = Send-JsonRpc "initialize" @{
        protocolVersion = "2024-11-05"
        capabilities    = @{}
        clientInfo      = @{ name = "TestClient"; version = "1.0" }
    } "init-1"
    $status -eq 202
}

Test-Check "POST /messages returns 202 for 'tools/list'" {
    $status = Send-JsonRpc "tools/list" @{} "list-1"
    $status -eq 202
}

Test-Check "POST /messages returns 202 for 'tools/call' get_active_document" {
    $status = Send-JsonRpc "tools/call" @{
        name      = "get_active_document"
        arguments = @{}
    } "call-1"
    $status -eq 202
}

Test-Check "POST /messages returns 202 for 'ping'" {
    $status = Send-JsonRpc "ping" @{} "ping-1"
    $status -eq 202
}

# ── 4. CORS / ngrok headers ────────────────────────────────────────────────
Write-Host "`n[4] CORS / ngrok headers"
$healthResp = Invoke-WebRequest "$BaseUrl/health" -UseBasicParsing -EA SilentlyContinue
Test-Check "Access-Control-Allow-Origin header present" {
    $healthResp.Headers["Access-Control-Allow-Origin"] -ne $null
}

$corsResp = Invoke-WebRequest "$BaseUrl/sse" -Method OPTIONS `
               -Headers @{ "Origin" = "https://claude.ai" } `
               -UseBasicParsing -EA SilentlyContinue
Test-Check "OPTIONS /sse returns 204" {
    $corsResp.StatusCode -eq 204
}

# ── Summary ────────────────────────────────────────────────────────────────
Write-Host "`n" + "=" * 50
Write-Host "Results: $pass passed, $fail failed" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })

if ($fail -eq 0) {
    Write-Host @"

All checks passed!  Your RevitMCP server is ready for ngrok.

Next steps:
  1. Run:  .\ngrok_setup.ps1
  2. Copy the HTTPS URL shown (e.g. https://xxxx.ngrok-free.app)
  3. In Claude → Settings → Integrations → Add MCP server:
         https://xxxx.ngrok-free.app/sse

"@ -ForegroundColor Green
} else {
    Write-Host @"

Some checks failed.  Common fixes:
  - Make sure Revit 2026 is open with a project loaded
  - Check MCP ribbon tab → Open MCP Panel → server status = Running
  - Ensure no firewall blocks port 5000

"@ -ForegroundColor Yellow
}
