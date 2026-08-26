# Build & run CustomerApp on Android emulator (Windows file-lock safe).
# Usage: powershell -ExecutionPolicy Bypass -File .\run_android.ps1

$ErrorActionPreference = "Continue"
$app = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $app

Write-Host "==> Stop leftover Java/Dart"
Get-Process java, dart -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

$target = "C:\Temp\carrental-customer-build"
New-Item -ItemType Directory -Force -Path $target | Out-Null
$link = Join-Path $app "build"
if (-not (Test-Path $link)) {
    cmd /c "mklink /J `"$link`" `"$target`"" | Out-Null
}

$max = 6
$ok = $false
for ($i = 1; $i -le $max; $i++) {
    Write-Host "==> Build attempt $i / $max"
    Get-Process java -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    # Clear only CMake intermediates that cause the lock
    Get-ChildItem -Path $target -Recurse -Directory -Filter "cxx" -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }

    flutter build apk --debug
    if ($LASTEXITCODE -eq 0) {
        $ok = $true
        break
    }
    Write-Host "Build failed (file lock). Retrying..."
    Start-Sleep -Seconds 3
}

if (-not $ok) {
    Write-Host @"

BUILD FAILED after $max tries.

Do this once (PowerShell Run as Administrator), then run this script again:

  Add-MpPreference -ExclusionPath 'C:\Temp\carrental-customer-build'
  Add-MpPreference -ExclusionPath '$app'
  Add-MpPreference -ExclusionProcess 'java.exe','cmake.exe','ninja.exe','dart.exe'

Or temporarily turn off real-time antivirus while building.

"@
    exit 1
}

Write-Host "==> Install & run on emulator"
flutter install
flutter run --use-application-binary=build\app\outputs\flutter-apk\app-debug.apk
