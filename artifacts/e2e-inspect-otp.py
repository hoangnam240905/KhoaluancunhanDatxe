import sqlite3
p = r"d:\DoAnCuNhan\CarRentalSystem\Backend\carrental.db"
out = r"d:\DoAnCuNhan\CarRentalSystem\artifacts\e2e-db-report.txt"
con = sqlite3.connect(p)
c = con.cursor()
lines = []
lines.append("users")
for r in c.execute("SELECT UserId, Email, RoleId, IsActive, IsLocked, IsEmailVerified, EmailVerifiedAt IS NOT NULL, GoogleSubject IS NOT NULL, length(FullName), Phone FROM Users ORDER BY UserId"):
    lines.append(str(r))
lines.append("customers")
for r in c.execute("SELECT CustomerId FROM Customers ORDER BY CustomerId"):
    lines.append(str(r))
lines.append("otp")
for r in c.execute("SELECT EmailOtpId, Email, Purpose, length(CodeHash), AttemptCount, UsedAt IS NOT NULL FROM EmailOtps ORDER BY EmailOtpId"):
    h = c.execute("SELECT CodeHash FROM EmailOtps WHERE EmailOtpId=?", (r[0],)).fetchone()[0]
    is_hex = all(ch in "0123456789ABCDEFabcdef" for ch in h) and len(h)==64
    plain6 = h.isdigit() and len(h)==6
    lines.append(str(r) + " hex64=" + str(is_hex) + " plain6=" + str(plain6))
con.close()
open(out, "w", encoding="utf-8").write("\n".join(lines))
print("wrote", out)
