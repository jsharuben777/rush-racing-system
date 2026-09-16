# start-tunnel-and-kiosk.ps1
#
# Automates: start cloudflared -> capture the new trycloudflare.com URL ->
# patch Kiosk's appsettings.json PublicBookingUrl -> start Web -> start Kiosk.
#
# Run this instead of manually starting things one by one. Every run gets a
# brand-new tunnel URL, and Kiosk's QR code is updated to match automatically.

$ErrorActionPreference = "Stop"

# ---- EDIT THESE IF YOUR FOLDER LAYOUT DIFFERS ----
$root          = "C:\Users\User\Desktop\rush_management_system-main\rush_management_system-main"
$webAppPath    = Join-Path $root "src\RushRacing.Web"
$kioskAppPath  = Join-Path $root "src\RushRacing.Kiosk"
$localPort     = 5233
$cloudflaredExe = "cloudflared"   # change to full .exe path if it's not on PATH
# ---------------------------------------------------

$kioskAppSettingsPath = Join-Path $kioskAppPath "appsettings.json"
if (-not (Test-Path $kioskAppSettingsPath)) {
    throw "Could not find Kiosk appsettings.json at: $kioskAppSettingsPath"
}

Write-Host "Starting Cloudflare quick tunnel on port $localPort ..." -ForegroundColor Cyan

$tunnelStdOutPath = Join-Path $env:TEMP "cloudflared-stdout.log"
$tunnelStdErrPath = Join-Path $env:TEMP "cloudflared-stderr.log"
Remove-Item $tunnelStdOutPath -ErrorAction SilentlyContinue
Remove-Item $tunnelStdErrPath -ErrorAction SilentlyContinue

$tunnelProcess = Start-Process -FilePath $cloudflaredExe `
    -ArgumentList "tunnel --url http://localhost:$localPort" `
    -NoNewWindow -PassThru `
    -RedirectStandardOutput $tunnelStdOutPath `
    -RedirectStandardError $tunnelStdErrPath

Write-Host "Waiting for tunnel URL to appear in cloudflared output..." -ForegroundColor Cyan

# cloudflared normally logs to stderr, but we check both to be safe.
$tunnelUrl = $null
$maxWaitSeconds = 30
$elapsed = 0

while (-not $tunnelUrl -and $elapsed -lt $maxWaitSeconds) {
    Start-Sleep -Seconds 1
    $elapsed++
    foreach ($path in @($tunnelStdErrPath, $tunnelStdOutPath)) {
        if (-not $tunnelUrl -and (Test-Path $path)) {
            $content = Get-Content $path -Raw -ErrorAction SilentlyContinue
            if ($content -match "https://[a-zA-Z0-9\-]+\.trycloudflare\.com") {
                $tunnelUrl = $matches[0]
            }
        }
    }
}

if (-not $tunnelUrl) {
    Write-Host "ERROR: No tunnel URL detected after $maxWaitSeconds seconds." -ForegroundColor Red
    Write-Host "Check the logs for details:"
    Write-Host "  $tunnelStdOutPath"
    Write-Host "  $tunnelStdErrPath"
    exit 1
}

Write-Host "Tunnel is live at: $tunnelUrl" -ForegroundColor Green

# --- Patch Kiosk's appsettings.json, keeping the existing path (e.g. /race/ck01) ---
Write-Host "Updating Kiosk appsettings.json ..." -ForegroundColor Cyan

$json = Get-Content $kioskAppSettingsPath -Raw | ConvertFrom-Json
$oldUri = [Uri]$json.RushRacing.PublicBookingUrl
$newPublicUrl = "$tunnelUrl$($oldUri.PathAndQuery)"
$json.RushRacing.PublicBookingUrl = $newPublicUrl
$json | ConvertTo-Json -Depth 10 | Set-Content $kioskAppSettingsPath -Encoding UTF8

Write-Host "PublicBookingUrl set to: $newPublicUrl" -ForegroundColor Green

# --- Start Web app in its own window ---
Write-Host "Starting RushRacing.Web ..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$webAppPath'; dotnet run"

Write-Host "Waiting a few seconds for Web to come up before starting Kiosk ..."
Start-Sleep -Seconds 8

# --- Start Kiosk app in its own window ---
Write-Host "Starting RushRacing.Kiosk ..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$kioskAppPath'; dotnet run"

Write-Host ""
Write-Host "All set." -ForegroundColor Green
Write-Host "cloudflared PID: $($tunnelProcess.Id) - keep it running, or the link dies."
Write-Host "Public URL for this session: $newPublicUrl"
