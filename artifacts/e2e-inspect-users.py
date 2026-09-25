import sqlite3
con = sqlite3.connect(r"d:\DoAnCuNhan\CarRentalSystem\Backend\carrental.db")
c = con.cursor()
print("users_e2e")
for r in c.execute("SELECT UserId,Email,FullName,Phone,RoleId,IsEmailVerified,EmailVerifiedAt,GoogleSubject FROM Users WHERE UserId>=8"):
    print(r)
print("customers_ids", list(c.execute("SELECT CustomerId FROM Customers ORDER BY CustomerId")))
print("orphan_customers", list(c.execute("SELECT CustomerId FROM Customers WHERE CustomerId NOT IN (SELECT UserId FROM Users)")))
print("customer_users_missing_row", list(c.execute("SELECT UserId,Email FROM Users WHERE RoleId=3 AND UserId NOT IN (SELECT CustomerId FROM Customers)")))
con.close()
