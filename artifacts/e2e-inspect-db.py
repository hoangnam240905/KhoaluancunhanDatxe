import sqlite3
import os
import sys

path = sys.argv[1] if len(sys.argv) > 1 else r"d:\DoAnCuNhan\CarRentalSystem\Backend\carrental.db"
print("db_path", path)
print("db_exists", os.path.exists(path))
if not os.path.exists(path):
    raise SystemExit(0)

con = sqlite3.connect(path)
c = con.cursor()
cols = [r[1] for r in c.execute("PRAGMA table_info(Users)")]
print("Users.columns", ",".join(cols))
tables = [r[0] for r in c.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")]
print("tables", ",".join(tables))
print("has_EmailOtps", "EmailOtps" in tables)
print("user_count", c.execute("SELECT COUNT(*) FROM Users").fetchone()[0])
print("customer_count", c.execute("SELECT COUNT(*) FROM Customers").fetchone()[0])
print("roles")
for r in c.execute("SELECT RoleId, RoleName FROM Roles"):
    print(tuple(r))
print("users_summary")
has_verified = "IsEmailVerified" in cols
has_google = "GoogleSubject" in cols
if has_verified and has_google:
    sql = "SELECT UserId, Email, RoleId, IsActive, IsLocked, IsEmailVerified, EmailVerifiedAt, GoogleSubject FROM Users ORDER BY UserId"
else:
    sql = "SELECT UserId, Email, RoleId, IsActive FROM Users ORDER BY UserId"
for r in c.execute(sql):
    print(tuple(r))
if "EmailOtps" in tables:
    print("otp_count", c.execute("SELECT COUNT(*) FROM EmailOtps").fetchone()[0])
    print("otp_meta")
    for r in c.execute(
        "SELECT EmailOtpId, Email, Purpose, length(CodeHash), AttemptCount, UsedAt IS NOT NULL, CreatedAt, ExpiresAt FROM EmailOtps ORDER BY EmailOtpId DESC LIMIT 20"
    ):
        print(tuple(r))
con.close()
