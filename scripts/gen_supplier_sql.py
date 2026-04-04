"""Generate SQL to insert missing suppliers from Access into Neon PostgreSQL."""
import pyodbc

ACCESS_PATH = r"C:\Users\Ivan\source\repos\Trendplus2\TRENDPLUS.accdb"
conn_str = f"DRIVER={{Microsoft Access Driver (*.mdb, *.accdb)}};DBQ={ACCESS_PATH};"
conn = pyodbc.connect(conn_str)
cur = conn.cursor()
cur.execute("SELECT IDDobavljac, Dobavljac, Adresa, BrTelDobav, Napomena FROM tblDobavljaci WHERE IDDobavljac < 0 ORDER BY IDDobavljac")
rows = cur.fetchall()
conn.close()

def esc(v):
    if v is None:
        return "NULL"
    s = str(v).strip()
    if not s:
        return "NULL"
    return "'" + s.replace("'", "''") + "'"

lines = []
lines.append(f"-- Insert {len(rows)} missing suppliers (negative IDs from Access Random Autonumber)")
lines.append("-- Run this in Neon SQL Editor")
lines.append("")
for r in rows:
    sid, name, addr, phone, note = r
    lines.append(
        f'INSERT INTO "Dobavljaci" ("Id", "Naziv", "Adresa", "Telefon", "Napomena", "DataOrigin") '
        f"VALUES ({sid}, {esc(name)}, {esc(addr)}, {esc(phone)}, {esc(note)}, 'access_fix') "
        f'ON CONFLICT ("Id") DO NOTHING;'
    )

lines.append("")
lines.append("-- Verification: should return 0")
lines.append('SELECT COUNT(*) AS orphan_articles FROM "Artikli" a LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id" WHERE a."IDDobavljac" IS NOT NULL AND d."Id" IS NULL;')

output = "\n".join(lines)
with open(r"C:\Users\Ivan\source\repos\Trendplus2\scripts\fix_missing_suppliers.sql", "w", encoding="utf-8") as f:
    f.write(output)

print(output)
print(f"\n-- Saved to scripts/fix_missing_suppliers.sql")
