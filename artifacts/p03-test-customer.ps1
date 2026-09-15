$ErrorActionPreference = 'Continue'
$base = 'http://127.0.0.1:5163'
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

function Get-Token([string]$html) {
  if ($html -match 'name="__RequestVerificationToken"[^>]*value="([^"]+)"') { return $Matches[1] }
  if ($html -match 'value="([^"]+)"[^>]*name="__RequestVerificationToken"') { return $Matches[1] }
  return $null
}

function Get-Hidden([string]$html, [string]$name) {
  $pattern1 = 'name="' + [regex]::Escape($name) + '"[^>]*value="([^"]*)"'
  $pattern2 = 'value="([^"]*)"[^>]*name="' + [regex]::Escape($name) + '"'
  if ($html -match $pattern1) { return $Matches[1] }
  if ($html -match $pattern2) { return $Matches[1] }
  return $null
}

try {
  Invoke-WebRequest -Uri "$base/Bookings/Create?slug=mercedes-benz-e-class" -WebSession $session -MaximumRedirection 0 -ErrorAction Stop | Out-Null
  Write-Output 'UNAUTHORIZED_STATUS=200'
} catch {
  $r = $_.Exception.Response
  $code = 0
  $loc = ''
  if ($r) {
    $code = [int]$r.StatusCode
    $loc = [string]$r.Headers['Location']
  }
  Write-Output "UNAUTHORIZED_STATUS=$code LOCATION=$loc"
}

$loginGet = Invoke-WebRequest -Uri "$base/Account/Login?returnUrl=%2FBookings%2FCreate%3Fslug%3Dmercedes-benz-e-class" -WebSession $session
$token = Get-Token $loginGet.Content
Write-Output "LOGIN_TOKEN_LEN=$($token.Length)"
$login = Invoke-WebRequest -Uri "$base/Account/Login" -WebSession $session -Method POST -Body @{
  'Input.Email' = 'customer1@gmail.com'
  'Input.Password' = 'Password123!'
  ReturnUrl = '/Bookings/Create?slug=mercedes-benz-e-class'
  '__RequestVerificationToken' = $token
}
Write-Output "LOGIN_URL=$($login.BaseResponse.ResponseUri.AbsoluteUri)"

$create = Invoke-WebRequest -Uri "$base/Bookings/Create?slug=mercedes-benz-e-class" -WebSession $session
$html = $create.Content
Write-Output "CREATE_URL=$($create.BaseResponse.ResponseUri.AbsoluteUri)"
Write-Output "HAS_FORM=$($html.Contains('data-cb-form'))"
Write-Output "HAS_SUBMIT=$($html.Contains('data-cb-confirm'))"
$typeId = Get-Hidden $html 'Input.VehicleTypeId'
$mode = Get-Hidden $html 'Input.RentalMode'
$fp = Get-Hidden $html 'QuotedFingerprint'
$qc = Get-Hidden $html 'QuoteConfirmed'
Write-Output "TYPE_ID=$typeId MODE=$mode QUOTE_CONFIRMED=$qc"
Write-Output "FINGERPRINT=$fp"
Write-Output "HAS_DRIVER_ROW=$($html.Contains('data-cb-driver-row'))"
Write-Output "HAS_MOCK_QUOTE=$($html.Contains('may chu'))"
Write-Output "HAS_REAL_QUOTE=$($html.Contains('data-cb-rental'))"

$start = '2026-09-20T10:00'
$end = '2026-09-23T10:00'
$q1 = Invoke-WebRequest -Uri "$base/Bookings/Create?handler=QuoteJson&vehicleTypeId=$typeId&startDate=$start&endDate=$end&rentalMode=WithDriver" -WebSession $session
Write-Output "QUOTE_WD=$($q1.StatusCode) $($q1.Content)"

$q2 = Invoke-WebRequest -Uri "$base/Bookings/Create?handler=QuoteJson&vehicleTypeId=$typeId&startDate=$start&endDate=$end&rentalMode=SelfDrive" -WebSession $session
Write-Output "QUOTE_SD=$($q2.StatusCode) $($q2.Content)"

try {
  Invoke-WebRequest -Uri "$base/Bookings/Create?handler=QuoteJson&vehicleTypeId=$typeId&startDate=$end&endDate=$start&rentalMode=WithDriver" -WebSession $session -ErrorAction Stop | Out-Null
  Write-Output 'QUOTE_BAD=200'
} catch {
  $resp = $_.Exception.Response
  $stream = $resp.GetResponseStream()
  $reader = New-Object System.IO.StreamReader($stream)
  $body = $reader.ReadToEnd()
  Write-Output "QUOTE_BAD=$([int]$resp.StatusCode) $body"
}

try {
  Invoke-WebRequest -Uri "$base/Bookings/Create?handler=QuoteJson&vehicleTypeId=99999&startDate=$start&endDate=$end&rentalMode=WithDriver" -WebSession $session -ErrorAction Stop | Out-Null
  Write-Output 'QUOTE_MISS=200'
} catch {
  $resp = $_.Exception.Response
  $stream = $resp.GetResponseStream()
  $reader = New-Object System.IO.StreamReader($stream)
  $body = $reader.ReadToEnd()
  Write-Output "QUOTE_MISS=$([int]$resp.StatusCode) $body"
}

$createToken = Get-Token $html
$wdFp = "$typeId|WithDriver||$start|$end|"
try {
  $wd = Invoke-WebRequest -Uri "$base/Bookings/Create?slug=mercedes-benz-e-class" -WebSession $session -Method POST -MaximumRedirection 0 -ErrorAction Stop -Body @{
    'Input.VehicleTypeId' = $typeId
    'Input.RentalMode' = 'WithDriver'
    'Input.PickupAddress' = 'Tan Son Nhat'
    'Input.DropoffAddress' = 'Tan Son Nhat'
    'Input.StartDate' = $start
    'Input.EndDate' = $end
    QuoteConfirmed = 'true'
    QuotedFingerprint = $wdFp
    FromRecommendation = 'false'
    '__RequestVerificationToken' = $createToken
  }
  Write-Output "CREATE_WD_STATUS=$($wd.StatusCode) URL=$($wd.BaseResponse.ResponseUri.AbsoluteUri)"
} catch {
  $r = $_.Exception.Response
  $loc = ''
  if ($r) { $loc = [string]$r.Headers['Location'] }
  Write-Output "CREATE_WD_STATUS=$([int]$r.StatusCode) LOC=$loc"
}

$create2 = Invoke-WebRequest -Uri "$base/Bookings/Create?slug=hyundai-accent" -WebSession $session
$html2 = $create2.Content
$typeId2 = Get-Hidden $html2 'Input.VehicleTypeId'
$token2 = Get-Token $html2
$sdStart = '2026-09-25T09:00'
$sdEnd = '2026-09-26T18:00'
$sdFp = "$typeId2|SelfDrive||$sdStart|$sdEnd|"
try {
  $sd = Invoke-WebRequest -Uri "$base/Bookings/Create?slug=hyundai-accent" -WebSession $session -Method POST -MaximumRedirection 0 -ErrorAction Stop -Body @{
    'Input.VehicleTypeId' = $typeId2
    'Input.RentalMode' = 'SelfDrive'
    'Input.PickupAddress' = 'Quan 1'
    'Input.DropoffAddress' = 'Quan 1'
    'Input.StartDate' = $sdStart
    'Input.EndDate' = $sdEnd
    QuoteConfirmed = 'true'
    QuotedFingerprint = $sdFp
    FromRecommendation = 'false'
    '__RequestVerificationToken' = $token2
  }
  Write-Output "CREATE_SD_STATUS=$($sd.StatusCode) URL=$($sd.BaseResponse.ResponseUri.AbsoluteUri)"
} catch {
  $r = $_.Exception.Response
  $loc = ''
  if ($r) { $loc = [string]$r.Headers['Location'] }
  Write-Output "CREATE_SD_STATUS=$([int]$r.StatusCode) LOC=$loc"
}

$create3 = Invoke-WebRequest -Uri "$base/Bookings/Create?slug=mercedes-benz-e-class" -WebSession $session
$html3 = $create3.Content
$token3 = Get-Token $html3
$typeId3 = Get-Hidden $html3 'Input.VehicleTypeId'
$bad = Invoke-WebRequest -Uri "$base/Bookings/Create?slug=mercedes-benz-e-class" -WebSession $session -Method POST -Body @{
  'Input.VehicleTypeId' = $typeId3
  'Input.RentalMode' = 'WithDriver'
  'Input.PickupAddress' = 'A'
  'Input.DropoffAddress' = 'A'
  'Input.StartDate' = $end
  'Input.EndDate' = $start
  QuoteConfirmed = 'true'
  QuotedFingerprint = "$typeId3|WithDriver||$end|$start|"
  FromRecommendation = 'false'
  '__RequestVerificationToken' = $token3
}
Write-Output "INVALID_DATES_PATH=$($bad.BaseResponse.ResponseUri.AbsolutePath)"
Write-Output "INVALID_HAS_ERROR=$($bad.Content.Contains('is-invalid'))"
Write-Output "INVALID_STAY=$($bad.BaseResponse.ResponseUri.AbsolutePath.Contains('/Bookings/Create'))"
