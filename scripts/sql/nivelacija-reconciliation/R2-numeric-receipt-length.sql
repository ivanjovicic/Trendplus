-- RQ547 / R2: detect numeric event receipt references too long for the current integer cast.
BEGIN TRANSACTION READ ONLY;

WITH numeric_receipts AS (
    SELECT d."Id",
           d."BrojRacuna",
           length(d."BrojRacuna")::integer AS raw_digit_count,
           COALESCE(NULLIF(ltrim(d."BrojRacuna", '0'), ''), '0') AS normalized_digits
    FROM "DnevnikPromena" d
    WHERE d."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
      AND d."BrojRacuna" ~ '^[0-9]+$'
), summary AS (
    SELECT COUNT(*)::bigint AS numeric_rows,
           COUNT(*) FILTER (WHERE length(normalized_digits) > 10
                               OR (length(normalized_digits) = 10 AND normalized_digits > '2147483647'))::bigint AS integer_overflow_rows,
           MAX(raw_digit_count) AS max_raw_digits,
           MAX(length(normalized_digits)) AS max_numeric_value_digits
    FROM numeric_receipts
)
SELECT
    'NV-P1-R2'::text AS check_id,
    CASE WHEN integer_overflow_rows = 0 THEN 'PASS' ELSE 'FAIL' END::text AS verdict,
    format('numeric_rows=%s; integer_overflow_rows=%s; max_raw_digits=%s; max_numeric_value_digits=%s',
           numeric_rows, integer_overflow_rows, COALESCE(max_raw_digits::text, 'n/a'), COALESCE(max_numeric_value_digits::text, 'n/a')) AS observed,
    'no numeric BrojRacuna exceeds the positive PostgreSQL integer cast bound'::text AS expected,
    'RQ544'::text AS owner,
    'Compares normalized digit strings without casting, so leading zeroes are not false overflow evidence.'::text AS detail
FROM summary;

ROLLBACK;
