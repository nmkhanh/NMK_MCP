# ============================================================
# ngrok_setup.ps1 (STABLE VERSION)
# ============================================================

param(
    [int]   $Port      = 5000,
    [string]$NgrokPath = "$env:LOCALAPPDATA\ngrok\ngrok.exe"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step($msg) {
    Write-Host "`n==> $msg" -ForegroundColor Cyan
}
function Write-Ok($msg) {
    Write-Host "    [OK] $msg" -ForegroundColor Green
}
function Write-Warn($msg) {
    Write-Host "    [!!] $msg" -ForegroundColor Yellow
}

# Step 1
Write-Step "Checking ngrok..."

if (-not (Test-Path $NgrokPath)) {
    Write-Warn "ngrok not found → downloading..."

    $zipUrl  = "https://bin.equinox.io/c/bNyj1mQVY4c/ngrok-v3-stable-windows-amd64.zip"
    $zipPath = "$env:TEMP\ngrok.zip"
    $ngrokDir = Split-Path $NgrokPath -Parent

    Invoke-WebRequest $zipUrl -OutFile $zipPath
    New-Item -ItemType Directory -Force -Path $ngrokDir | Out-Null
    Expand-Archive $zipPath -DestinationPath $ngrokDir -Force
    Remove-Item $zipPath

    Write-Ok "ngrok installed"
} else {
    Write-Ok "ngrok found"
}

# Step 2
Write-Step "Version check..."
& $NgrokPath version

# Step 3
Write-Step "Auth token..."

$configPath = "$env:USERPROFILE\.ngrok2\ngrok.yml"

if (-not (Test-Path $configPath)) {
    Write-Warn "No token"

    $token = Read-Host "Paste token"
    if ($token) {
        & $NgrokPath config add-authtoken $token
        Write-Ok "Token saved"
    }
} else {
    Write-Ok "Token exists"
}

# Step 4
Write-Step "Checking MCP..."

try {
    Invoke-WebRequest "http://localhost:$Port/sse" -TimeoutSec 3 | Out-Null
    Write-Ok "MCP OK"
} catch {
    Write-Warn "MCP not reachable"
    $c = Read-Host "Continue? (y/N)"
    if ($c -ne 'y') { exit }
}

# Step 5
Write-Step "Starting tunnel..."

Write-Host "👉 Copy HTTPS URL + /sse → paste vào Claude"

& $NgrokPath http $Port