$ErrorActionPreference = 'Stop'

$adb = Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'
if (-not (Test-Path $adb)) {
    throw "Khong tim thay adb tai $adb"
}

flutter emulators --launch Pixel_5
& $adb -s emulator-5554 wait-for-device

for ($i = 0; $i -lt 60; $i++) {
    $booted = (& $adb -s emulator-5554 shell getprop sys.boot_completed 2>$null).Trim()
    if ($booted -eq '1') {
        & $adb -s emulator-5554 shell settings put global window_animation_scale 0
        & $adb -s emulator-5554 shell settings put global transition_animation_scale 0
        & $adb -s emulator-5554 shell settings put global animator_duration_scale 0
        exit 0
    }
    Start-Sleep -Seconds 1
}

throw 'Android emulator khong khoi dong xong trong 60 giay.'
