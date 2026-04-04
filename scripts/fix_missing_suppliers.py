"""
Fix missing suppliers: Extract negative-ID suppliers from Access DB
and insert them into PostgreSQL where they are missing.
"""
import pyodbc
import psycopg2

ACCESS_PATH = r"C:\Users\Ivan\source\repos\Trendplus2\TRENDPLUS.accdb"
PG_CONN = "host=127.0.0.1 port=5434 dbname=trendplus user=postgres password=postgres"

# 1. Read all suppliers from Access
access_conn_str = (
    r"DRIVER={Microsoft Access Driver (*.mdb, *.accdb)};"
    f"DBQ={ACCESS_PATH};"
)
access_conn = pyodbc.connect(access_conn_str)
access_cur = access_conn.cursor()
access_cur.execute("SELECT IDDobavljac, Dobavljac, Adresa, BrTelDobav, Napomena FROM tblDobavljaci")
access_suppliers = access_cur.fetchall()
access_conn.close()

print(f"Access DB: {len(access_suppliers)} total suppliers")

# 2. Connect to PostgreSQL and find which IDs are missing
pg_conn = psycopg2.connect(PG_CONN)
pg_cur = pg_conn.cursor()

pg_cur.execute('SELECT "Id" FROM "Dobavljaci"')
existing_ids = {row[0] for row in pg_cur.fetchall()}
print(f"PostgreSQL: {len(existing_ids)} existing suppliers")

# 3. Find missing suppliers
missing = []
for row in access_suppliers:
    sid, name, addr, phone, note = row
    if sid not in existing_ids:
        missing.append((sid, name, addr, phone, note))

print(f"Missing suppliers to insert: {len(missing)}")

if not missing:
    print("Nothing to do!")
    pg_conn.close()
    exit(0)

# 4. Insert missing suppliers
insert_sql = """
    INSERT INTO "Dobavljaci" ("Id", "Naziv", "Adresa", "Telefon", "Napomena", "DataOrigin")
    VALUES (%s, %s, %s, %s, %s, 'access_import')
    ON CONFLICT ("Id") DO NOTHING
"""

inserted = 0
for sid, name, addr, phone, note in missing:
    # Clean up values
    name = (name or "").strip() or None
    addr = (addr or "").strip() or None
    phone = (phone or "").strip() or None
    note = (note or "").strip() or None
    
    pg_cur.execute(insert_sql, (sid, name, addr, phone, note))
    inserted += pg_cur.rowcount

pg_conn.commit()
print(f"Inserted {inserted} suppliers")

# 5. Verify: check orphan articles
pg_cur.execute("""
    SELECT COUNT(*) FROM "Artikli" a
    LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
    WHERE a."IDDobavljac" IS NOT NULL AND d."Id" IS NULL
""")
orphans = pg_cur.fetchone()[0]
print(f"Remaining orphan articles (should be 0): {orphans}")

pg_conn.close()
print("Done!")
