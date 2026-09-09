import sqlite3

path = r"d:\DoAnCuNhan\CarRentalSystem\Backend\carrental.db"
con = sqlite3.connect(path)
c = con.cursor()

print("=== USERS ===")
for r in c.execute(
    """
    SELECT UserId, Email, RoleId, IsActive, IsLocked, IsEmailVerified,
           EmailVerifiedAt IS NOT NULL, GoogleSubject IS NOT NULL,
           length(PasswordHash)
    FROM Users ORDER BY UserId
    """
):
    print(r)

print("=== CUSTOMERS ===")
for r in c.execute("SELECT CustomerId, UserId, FullName, Phone FROM Customers ORDER BY CustomerId"):
    print(r)

print("=== DUP USERS ===")
for r in c.execute("SELECT Email, COUNT(*) FROM Users GROUP BY Email HAVING COUNT(*)>1"):
    print(r)
print("=== DUP CUSTOMERS ===")
for r in c.execute("SELECT UserId, COUNT(*) FROM Customers GROUP BY UserId HAVING COUNT(*)>1"):
    print(r)

print("=== OTP META (no plaintext) ===")
otp_cols = [r[1] for r in c.execute("PRAGMA table_info(EmailOtps)")]
print("otp_columns", otp_cols)
plaintext = [col for col in otp_cols if col.lower() in ("code", "otp", "plaintext", "otpcode")]
print("plaintext_otp_columns", plaintext or "none")
for r in c.execute(
    """
    SELECT EmailOtpId, Email, Purpose, length(CodeHash),
           CASE WHEN CodeHash GLOB '[0-9][0-9][0-9][0-9][0-9][0-9]' THEN 1 ELSE 0 END as looks_plain,
           AttemptCount, UsedAt IS NOT NULL, CreatedAt, ExpiresAt
    FROM EmailOtps ORDER BY EmailOtpId
    """
):
    print(r)

print("=== FORGOT customer1 last 5 ===")
for r in c.execute(
    """
    SELECT EmailOtpId, length(CodeHash), AttemptCount, UsedAt IS NOT NULL, CreatedAt
    FROM EmailOtps WHERE Email='customer1@gmail.com' AND Purpose='PasswordReset'
    ORDER BY EmailOtpId DESC LIMIT 5
    """
):
    print(r)

print("=== e2e.ui.portal08 ===")
for r in c.execute(
    """
    SELECT EmailOtpId, Purpose, length(CodeHash), AttemptCount, UsedAt IS NOT NULL, CreatedAt, ExpiresAt
    FROM EmailOtps WHERE Email='e2e.ui.portal08@gmail.com' ORDER BY EmailOtpId
    """
):
    print(r)

print("=== roles of seed ===")
for r in c.execute(
    "SELECT Email, RoleId FROM Users WHERE Email IN ('admin@carrental.vn','dispatcher@carrental.vn','driver1@carrental.vn','customer1@gmail.com')"
):
    print(r)
con.close()
