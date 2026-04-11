-- Check comparable articles with both pre and post niv sales
WITH first_nivelacija AS (
    SELECT "ArtikalId" as artikal_id, MIN("Datum") as prva_datum
    FROM "DnevnikPromena"
    WHERE ("TipPromene" = 'Nivelacija' OR "TipPromene" = 'NivelacijaCena')
      AND "ArtikalId" IS NOT NULL
    GROUP BY "ArtikalId"
),
article_splits AS (
    SELECT 
        a."Id" as artikal_id,
        SUM(CASE WHEN pz.datum_prodaje < fn.prva_datum THEN ps.kolicina * ps.cena ELSE 0 END) as pre_rev,
        SUM(CASE WHEN pz.datum_prodaje >= fn.prva_datum THEN ps.kolicina * ps.cena ELSE 0 END) as post_rev,
        SUM(CASE WHEN pz.datum_prodaje < fn.prva_datum THEN ps.kolicina ELSE 0 END) as pre_qty,
        SUM(CASE WHEN pz.datum_prodaje >= fn.prva_datum THEN ps.kolicina ELSE 0 END) as post_qty
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    JOIN "Artikli" a ON ps.id_artikal = a."Id"
    JOIN first_nivelacija fn ON a."Id" = fn.artikal_id
    WHERE a."IDDobavljac" = 1496651500
      AND pz.datum_prodaje >= (CURRENT_DATE - INTERVAL '89 days')
      AND pz.datum_prodaje <= (CURRENT_DATE::timestamp + INTERVAL '23 hours 59 minutes 59 seconds')
    GROUP BY a."Id"
)
SELECT 
    COUNT(*) as total_niv_articles,
    SUM(CASE WHEN pre_rev > 0 AND post_rev > 0 AND pre_qty > 0 AND post_qty > 0 THEN 1 ELSE 0 END) as comparable_articles,
    SUM(CASE WHEN pre_rev > 0 AND post_rev > 0 AND pre_qty > 0 AND post_qty > 0 THEN pre_rev ELSE 0 END) as comparable_pre_rev,
    SUM(CASE WHEN pre_rev > 0 AND post_rev > 0 AND pre_qty > 0 AND post_qty > 0 THEN post_rev ELSE 0 END) as comparable_post_rev,
    ROUND(
        CASE WHEN SUM(CASE WHEN pre_rev > 0 AND post_rev > 0 AND pre_qty > 0 AND post_qty > 0 THEN pre_rev ELSE 0 END) > 0
        THEN ((SUM(CASE WHEN pre_rev > 0 AND post_rev > 0 AND pre_qty > 0 AND post_qty > 0 THEN post_rev ELSE 0 END) -
              SUM(CASE WHEN pre_rev > 0 AND post_rev > 0 AND pre_qty > 0 AND post_qty > 0 THEN pre_rev ELSE 0 END))::numeric /
              SUM(CASE WHEN pre_rev > 0 AND post_rev > 0 AND pre_qty > 0 AND post_qty > 0 THEN pre_rev ELSE 0 END)::numeric * 100)
        ELSE NULL END, 2) as impact_pct,
    SUM(pre_rev) as total_pre_rev,
    SUM(post_rev) as total_post_rev
FROM article_splits;



