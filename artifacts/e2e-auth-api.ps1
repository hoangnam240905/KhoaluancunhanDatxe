$base = "http://localhost:5199"
function Req($method, $path, $json) {
  $tmp = Join-Path $env:TEMP ("e2e-" + [guid]::NewGuid().ToString("N") + ".txt")
  if ($json) {
    $bodyFile = Join-Path $env:TEMP ("e2e-body-" + [guid]::NewGuid().ToString("N") + ".json")
    [IO.File]::WriteAllText($bodyFile, $json)
    $code = & curl.exe -s -o $tmp -w "%{http_code}" -X $method "$base$path" -H "Content-Type: application/json" --data-binary "@$bodyFile"
    Remove-Item $bodyFile -ErrorAction SilentlyContinue
  } else {
    $code = & curl.exe -s -o $tmp -w "%{http_code}" -X $method "$base$path"
  }
  $content = Get-Content -Raw -Path $tmp -ErrorAction SilentlyContinue
  Remove-Item $tmp -ErrorAction SilentlyContinue
  [pscustomobject]@{ Status = [int]$code; Content = $content }
}
function Log($n,$r,$d) { Write-Output ("{0}`t{1}`t{2}" -f $n,$r,$d) }
function Leak($text,$pwd) {
  if ([string]::IsNullOrEmpty($text)) { return $false }
  if ($text -match '(?i)"otp"\s*:') { return $true }
  if ($text -match '(?i)passwordHash') { return $true }
  if ($pwd -and $text.Contains($pwd)) { return $true }
  return $false
}

$opt = Req GET /api/auth/login-options $null
$oj = $opt.Content | ConvertFrom-Json
Log "login-options" ($(if ($opt.Status -eq 200) {"PASS"} else {"FAIL"})) "googleEnabled=$($oj.googleEnabled); clientIdSet=$([bool]$oj.googleClientId)"

function LoginCheck($email,$role) {
  $r = Req POST /api/auth/login (@{email=$email; password="Password123!"} | ConvertTo-Json -Compress)
  $ok = $r.Status -eq 200
  $claims = ""
  $roleOk = $false
  if ($ok) {
    $j = $r.Content | ConvertFrom-Json
    $roleOk = $j.role -eq $role
    $p = $j.token.Split(".")[1].Replace("-","+").Replace("_","/")
    switch ($p.Length % 4) { 2 { $p += "==" } 3 { $p += "=" } }
    $payload = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p))
    $claims = "jsonRole=$($j.role) hasNameId=$($payload -match "nameidentifier") hasEmail=$($payload -match "emailaddress") hasName=$($payload -match '"name"') hasRole=$($payload -match "role")"
  }
  Log "login-$role" ($(if ($ok -and $roleOk) {"PASS"} else {"FAIL"})) "status=$($r.Status) $claims"
}

LoginCheck "admin@carrental.vn" "Admin"
LoginCheck "dispatcher@carrental.vn" "Dispatcher"
LoginCheck "driver1@carrental.vn" "Driver"
LoginCheck "customer1@gmail.com" "Customer"

$email = "e2e.auth.$([guid]::NewGuid().ToString("N").Substring(0,10))@gmail.com"
$phone = ("0988{0:D6}" -f (Get-Random -Maximum 999999))
$pwd = "Password123!"
$reg = Req POST /api/auth/register (@{email=$email; password=$pwd; fullName="E2e Auth Tester"; phone=$phone; confirmPassword=$pwd} | ConvertTo-Json -Compress)
$rj = $null; try { $rj = $reg.Content | ConvertFrom-Json } catch {}
$hasToken = $reg.Content -match '(?i)"token"'
Log "register-new" ($(if ($reg.Status -eq 200 -and $rj.requiresVerification -eq $true -and -not $hasToken -and -not (Leak $reg.Content $pwd)) {"PASS"} else {"FAIL"})) "status=$($reg.Status) requiresVerification=$($rj.requiresVerification) hasToken=$hasToken leak=$(Leak $reg.Content $pwd)"

$dup = Req POST /api/auth/register (@{email="customer1@gmail.com"; password=$pwd; fullName="X"; phone="0912000099"; confirmPassword=$pwd} | ConvertTo-Json -Compress)
Log "register-duplicate" ($(if ($dup.Status -eq 400) {"PASS"} else {"FAIL"})) "status=$($dup.Status)"

$unv = Req POST /api/auth/login (@{email=$email; password=$pwd} | ConvertTo-Json -Compress)
Log "unverified-login" ($(if ($unv.Status -eq 403) {"PASS"} else {"FAIL"})) "status=$($unv.Status)"

$wrong = Req POST /api/auth/verify-email (@{email=$email; otp="000000"} | ConvertTo-Json -Compress)
Log "verify-wrong-otp" ($(if ($wrong.Status -eq 400 -and -not (Leak $wrong.Content $pwd)) {"PASS"} else {"FAIL"})) "status=$($wrong.Status) leak=$(Leak $wrong.Content $pwd)"

$resend = Req POST /api/auth/resend-verification-otp (@{email=$email} | ConvertTo-Json -Compress)
Log "resend" "INFO" "status=$($resend.Status) hasOtpField=$($resend.Content -match '(?i)\"otp\"')"

$fp1 = Req POST /api/auth/forgot-password (@{email="customer1@gmail.com"} | ConvertTo-Json -Compress)
$fp2 = Req POST /api/auth/forgot-password (@{email="nobody.missing@gmail.com"} | ConvertTo-Json -Compress)
$same = ($fp1.Status -eq $fp2.Status) -and ($fp1.Content -eq $fp2.Content)
Log "forgot-enumeration" ($(if ($fp1.Status -eq 200 -and $same -and -not (Leak $fp1.Content $pwd)) {"PASS"} else {"FAIL"})) "status=$($fp1.Status)/$($fp2.Status) same=$same leak=$(Leak $fp1.Content $pwd)"

$rstWrong = Req POST /api/auth/reset-password (@{email="customer1@gmail.com"; otp="000000"; newPassword="NewPass123!"; confirmPassword="NewPass123!"} | ConvertTo-Json -Compress)
Log "reset-wrong-otp" ($(if ($rstWrong.Status -eq 400) {"PASS"} else {"FAIL"})) "status=$($rstWrong.Status)"

$rstWeak = Req POST /api/auth/reset-password (@{email="customer1@gmail.com"; otp="123456"; newPassword="weak"; confirmPassword="weak"} | ConvertTo-Json -Compress)
Log "reset-weak-password" ($(if ($rstWeak.Status -eq 400) {"PASS"} else {"FAIL"})) "status=$($rstWeak.Status)"

$rstMismatch = Req POST /api/auth/reset-password (@{email="customer1@gmail.com"; otp="123456"; newPassword="NewPass123!"; confirmPassword="OtherPass123!"} | ConvertTo-Json -Compress)
Log "reset-confirm-mismatch" ($(if ($rstMismatch.Status -eq 400) {"PASS"} else {"FAIL"})) "status=$($rstMismatch.Status)"

$gInv = Req POST /api/auth/google (@{idToken="not-a-real-token"} | ConvertTo-Json -Compress)
Log "google-invalid-token" ($(if ($gInv.Status -eq 401 -or $gInv.Status -eq 503) {"PASS"} else {"FAIL"})) "status=$($gInv.Status)"

Write-Output "REGISTER_EMAIL=$email"
Write-Output "REGISTER_PHONE=$phone"
