# Redirect CustomerApp/build -> C:\Temp\carrental-customer-build
# so Flutter finds APK while Gradle writes outside Documents (avoids Defender locks).

$ErrorActionPreference = "Stop"
$appRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$linkPath = Join-Path $appRoot "build"
$target = "C:\Temp\carrental-customer-build"

New-Item -ItemType Directory -Force -Path $target | Out-Null

# Stop processes that lock build files
Get-Process java, dart, gradle -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

if (Test-Path $linkPath) {
    $item = Get-Item $linkPath -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        cmd /c "rmdir `"$linkPath`""
    } else {
        Remove-Item -LiteralPath $linkPath -Recurse -Force
    }
}

cmd /c "mklink /J `"$linkPath`" `"$target`""
Write-Host "OK: $linkPath -> $target"

# Best-effort Defender exclusions (needs Admin; ignore failures)
try {
    Add-MpPreference -ExclusionPath $target -ErrorAction Stop
    Add-MpPreference -ExclusionPath $appRoot -ErrorAction Stop
    Write-Host "OK: Windows Defender exclusions added"
} catch {
    Write-Host "NOTE: Run PowerShell as Admin to add Defender exclusions:"
    Write-Host "  Add-MpPreference -ExclusionPath '$target'"
    Write-Host "  Add-MpPreference -ExclusionPath '$appRoot'"
}
