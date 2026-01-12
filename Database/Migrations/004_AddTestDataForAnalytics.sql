-- PREPORU?ENO: Pokrenite 004_SimpleTestData.sql umesto ove skripte!
-- Ova skripta zahteva sp_prodaj_artikle_json stored procedure koja ne postoji.

-- Umesto ovoga, pokrenite:
-- psql -d trendplus_db -f Database/Migrations/004_SimpleTestData.sql

-- Ili pogledajte QUICKSTART.md za brzi start!

RAISE EXCEPTION 'STOP! Koristite 004_SimpleTestData.sql umesto ove skripte. Ova skripta zahteva stored procedure koja ne postoji u bazi.';
