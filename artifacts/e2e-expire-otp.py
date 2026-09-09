import sqlite3

path = r"d:\DoAnCuNhan\CarRentalSystem\Backend\carrental.db"
con = sqlite3.connect(path)
c = con.cursor()
c.execute(
    """
    UPDATE EmailOtps
    SET ExpiresAt = datetime('now', '-10 minutes')
    WHERE Email = ?
      AND Purpose = 'EmailVerification'
      AND UsedAt IS NULL
    """,
    ("e2e.auth.becca49609@gmail.com",),
)
print("expired_rows", c.rowcount)
con.commit()
for r in c.execute(
    """
    SELECT EmailOtpId, Purpose, length(CodeHash), AttemptCount,
           UsedAt IS NOT NULL, CreatedAt, ExpiresAt
    FROM EmailOtps
    WHERE Email = ?
    ORDER BY EmailOtpId
    """,
    ("e2e.auth.becca49609@gmail.com",),
):
    print(r)
con.close()
