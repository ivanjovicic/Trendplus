# 🌍 Global Trends Tracking System

AI-powered system za praćenje EU i social media trendova i mapiranje na lokalni inventar.

## 📊 Tabele

### 1. **EuTrends** - EU Fashion Trendovi
Prati trending proizvode iz evropskih marketa (Zalando, ASOS, About You, itd.)

**Kolone:**
- `ProductName` - Naziv trending proizvoda
- `Brand` - Brend (Nike, Adidas, ...)
- `Category` - Kategorija (Patike, Sandale, ...)
- `Color` - Boja
- `Rank` - Trend ranking (1 = najpopularniji)
- `Price` - Cena u EUR
- `Season` - Sezona
- `ImageUrl` - Link ka slici
- `Embedding` - 512D vektor za similarity search

### 2. **SocialTrends** - Social Media Trendovi
Prati hashtag-ove i growth na TikTok, Instagram, Pinterest

**Kolone:**
- `Category` - Kategorija
- `Hashtag` - #sneakerhead, #airmax, ...
- `PostsThisMonth` - Broj postova ovog meseca
- `PostsLastMonth` - Broj postova prošlog meseca
- `TiktokGrowth` - % rasta na TikToku
- `InstagramGrowth` - % rasta na Instagramu
- `PinterestGrowth` - % rasta na Pinterestu

### 3. **GlobalTrendScores** - Mapiranje na lokalne proizvode
Mapira tvoje proizvode na globalne trendove sa AI scoring-om

**Individual Scores (0-100):**
- `EuTrendScore` - Koliko tvoj proizvod prati EU trendove
- `SocialTrendScore` - Koliko je relevantan na social media
- `SimilarityScore` - Vizuelna sličnost sa trending proizvodima
- `ColorScore` - Da li je boja u trendu
- `PriceScore` - Da li je cena kompetitivna
- `SeasonScore` - Da li je sezonski relevantan

**Finalni Score:**
```
FinalGlobalScore = 
    EuTrendScore     * 0.30 +  // 30%
    SocialTrendScore * 0.25 +  // 25%
    SimilarityScore  * 0.20 +  // 20%
    ColorScore       * 0.10 +  // 10%
    PriceScore       * 0.10 +  // 10%
    SeasonScore      * 0.05    //  5%
```

### 4. **TrendHistory** - Istorija trendova
Prati kako se score menja tokom vremena

## 🚀 Setup

### 1. **Kreiranje tabela**
```bash
# Windows
cd Database\Analytics
apply-global-trends.bat

# Linux/Mac
psql "postgresql://user:pass@host:port/analytics?sslmode=require" -f 003_AddGlobalTrendsTables.sql
```

### 2. **Verifikacija**
```sql
-- Proveri da su tabele kreirane
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' 
AND table_name IN ('EuTrends', 'SocialTrends', 'GlobalTrendScores', 'TrendHistory');

-- Proveri sample data
SELECT * FROM "EuTrends" LIMIT 5;
SELECT * FROM "SocialTrends" LIMIT 5;
```

## 🔍 Primeri Upita

### Top Trending Categories
```sql
SELECT * FROM get_top_trending_categories(10);
```

### Pronađi slične EU trendove
```sql
-- Za proizvod sa embedding vektorom
SELECT * FROM find_similar_eu_trends(
    '[0.1, 0.2, ...]'::vector(512),  -- embedding vector
    0.7,  -- threshold
    5     -- max results
);
```

### Proizvodi sa najboljim trend score-om
```sql
SELECT 
    g."ProductName",
    g."FinalGlobalScore",
    g."EuTrendScore",
    g."SocialTrendScore",
    g."Recommendations"
FROM "GlobalTrendScores" g
ORDER BY g."FinalGlobalScore" DESC
LIMIT 10;
```

### Trend growth over time
```sql
SELECT 
    "LocalProductId",
    "Date",
    "FinalGlobalScore",
    LAG("FinalGlobalScore") OVER (
        PARTITION BY "LocalProductId" 
        ORDER BY "Date"
    ) AS "PreviousScore"
FROM "TrendHistory"
WHERE "LocalProductId" = 123
ORDER BY "Date" DESC;
```

## 📈 Kako Popuniti Podatke

### 1. **EU Trendovi** (Scraping)
```python
# Scrape trending products from EU sites
# Example: Zalando API, ASOS API, Google Shopping API

import requests

def fetch_zalando_trends():
    response = requests.get("https://api.zalando.com/trends")
    products = response.json()
    
    for product in products:
        # Generate embedding using CLIP
        embedding = generate_clip_embedding(product['image_url'])
        
        # Insert into database
        insert_eu_trend(
            product_name=product['name'],
            brand=product['brand'],
            category=product['category'],
            price=product['price'],
            embedding=embedding
        )
```

### 2. **Social Trendovi** (API)
```python
# Use TikTok, Instagram, Pinterest APIs
# Example: RapidAPI, Apify

def fetch_tiktok_trends():
    hashtags = ['#sneakerhead', '#airmax', '#yeezy']
    
    for hashtag in hashtags:
        data = tiktok_api.get_hashtag_stats(hashtag)
        
        insert_social_trend(
            category='Patike',
            hashtag=hashtag,
            posts_this_month=data['current_posts'],
            posts_last_month=data['previous_posts'],
            tiktok_growth=data['growth_percent']
        )
```

### 3. **Automatski Scoring** (Background Worker)
```csharp
// C# Background Worker
public class TrendScoringWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Za svaki artikal iz inventory-a
            var artikli = await _db.Artikli.ToListAsync(ct);
            
            foreach (var artikal in artikli)
            {
                // 1. Generate embedding
                var embedding = await _embeddingService.GetEmbeddingAsync(artikal.ImagePath);
                
                // 2. Find similar EU trends
                var similarTrends = await FindSimilarEuTrends(embedding);
                
                // 3. Calculate scores
                var score = await CalculateTrendScores(artikal, similarTrends);
                
                // 4. Save to GlobalTrendScores
                await SaveTrendScore(score);
            }
            
            await Task.Delay(TimeSpan.FromHours(6), ct); // Run every 6 hours
        }
    }
}
```

## 🎯 Use Cases

### 1. **Procurement Planning**
```sql
-- Proizvodi sa visokim trend score-om ali niskim inventory-jem
SELECT 
    a."Naziv",
    a."Kolicina",
    g."FinalGlobalScore",
    g."Recommendations"
FROM "Artikli" a
JOIN "GlobalTrendScores" g ON a."Id" = g."LocalProductId"
WHERE g."FinalGlobalScore" > 70
  AND a."Kolicina" < 5
ORDER BY g."FinalGlobalScore" DESC;
```

### 2. **Price Optimization**
```sql
-- Uporedi tvoje cene sa EU market average
SELECT 
    a."Naziv",
    a."ProdajnaCena" AS "MojaCena",
    AVG(et."Price") AS "EUAvgPrice",
    g."PriceScore"
FROM "Artikli" a
JOIN "GlobalTrendScores" g ON a."Id" = g."LocalProductId"
JOIN "EuTrends" et ON et."Category" = a."Kategorija"
GROUP BY a."Id", a."Naziv", a."ProdajnaCena", g."PriceScore"
HAVING g."PriceScore" < 50; -- Moje cene nisu kompetitivne
```

### 3. **Marketing Insights**
```sql
-- Hashtags za promociju na osnovu trending-a
SELECT 
    a."Naziv",
    g."MatchedHashtags",
    st."PostsThisMonth",
    st."TiktokGrowth"
FROM "Artikli" a
JOIN "GlobalTrendScores" g ON a."Id" = g."LocalProductId"
JOIN "SocialTrends" st ON st."Hashtag" = ANY(g."MatchedHashtags")
WHERE st."TiktokGrowth" > 20
ORDER BY st."TiktokGrowth" DESC;
```

## 🔧 Maintenance

### Arhiviranje starih podataka
```sql
-- Automatski obriši podatke starije od 90 dana
SELECT archive_old_trends();
```

### Ručno ažuriranje
```sql
-- Ručno update trending proizvoda
UPDATE "EuTrends"
SET "Rank" = "Rank" + 1
WHERE "UpdatedAt" < NOW() - INTERVAL '7 days';

-- Recalculate scores
UPDATE "GlobalTrendScores"
SET 
    "FinalGlobalScore" = calculate_final_trend_score(
        "EuTrendScore",
        "SocialTrendScore",
        "SimilarityScore",
        "ColorScore",
        "PriceScore",
        "SeasonScore"
    ),
    "UpdatedAt" = NOW();
```

## 📊 Dashboard Ideas

1. **Trend Heatmap** - Mapa kategorija sa highest growth
2. **My Products vs Market** - Uporedi svoj inventory sa EU trendovima
3. **Social Media Pulse** - Realtime hashtag tracking
4. **Price Competitiveness** - Gde možeš da spustiš/digneš cene
5. **Reorder Recommendations** - Šta da naručiš sledeće

## 🚨 Napomene

- **pgvector extension** mora biti installovan za similarity search
- **Embedding generation** zahteva Python service (CLIP model)
- **EU trend data** se mora scrape-ovati ili kupiti API pristup
- **Social media data** - koristi RapidAPI ili Apify za TikTok/Instagram

## 📚 Reference

- [pgvector dokumentacija](https://github.com/pgvector/pgvector)
- [CLIP model za embeddings](https://github.com/openai/CLIP)
- [Zalando API](https://www.zalando.com/api)
- [TikTok API](https://developers.tiktok.com/)
