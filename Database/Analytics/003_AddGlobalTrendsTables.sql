-- ============================================
-- Global Trends Tables for Analytics Database
-- ============================================
-- These tables track EU fashion trends and social media trends
-- to help predict demand and optimize inventory

-- Enable pgvector extension for similarity search
CREATE EXTENSION IF NOT EXISTS vector;

-- ============================================
-- 1. EU TRENDS TABLE
-- Stores trending products from European markets
-- ============================================
CREATE TABLE IF NOT EXISTS "EuTrends" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProductName" TEXT NOT NULL,
    "Brand" TEXT,
    "Category" TEXT,
    "Color" TEXT,
    "Rank" INT,
    "Price" DECIMAL(18,2),
    "Season" TEXT,
    "ImageUrl" TEXT,
    "Embedding" vector(512), -- Vector embedding for similarity matching
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Indexes for EuTrends
CREATE INDEX IF NOT EXISTS "IX_EuTrends_Category" ON "EuTrends" ("Category");
CREATE INDEX IF NOT EXISTS "IX_EuTrends_Brand" ON "EuTrends" ("Brand");
CREATE INDEX IF NOT EXISTS "IX_EuTrends_Rank" ON "EuTrends" ("Rank");
CREATE INDEX IF NOT EXISTS "IX_EuTrends_Season" ON "EuTrends" ("Season");
CREATE INDEX IF NOT EXISTS "IX_EuTrends_UpdatedAt" ON "EuTrends" ("UpdatedAt" DESC);

-- HNSW index for fast vector similarity search on embeddings
CREATE INDEX IF NOT EXISTS "IX_EuTrends_Embedding_HNSW" 
    ON "EuTrends" 
    USING hnsw ("Embedding" vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);

COMMENT ON TABLE "EuTrends" IS 'Trending products from European fashion markets';
COMMENT ON COLUMN "EuTrends"."Embedding" IS '512-dimensional vector for similarity matching with local products';
COMMENT ON COLUMN "EuTrends"."Rank" IS 'Trend ranking (1 = most trending)';


-- ============================================
-- 2. SOCIAL TRENDS TABLE
-- Tracks social media trends (TikTok, Instagram, etc.)
-- ============================================
CREATE TABLE IF NOT EXISTS "SocialTrends" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Category" TEXT NOT NULL,
    "Hashtag" TEXT NOT NULL,
    "PostsThisMonth" INT NOT NULL DEFAULT 0,
    "PostsLastMonth" INT NOT NULL DEFAULT 0,
    "TiktokGrowth" FLOAT, -- Growth percentage
    "InstagramGrowth" FLOAT,
    "PinterestGrowth" FLOAT,
    "AverageEngagement" FLOAT,
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    
    UNIQUE("Category", "Hashtag")
);

-- Indexes for SocialTrends
CREATE INDEX IF NOT EXISTS "IX_SocialTrends_Category" ON "SocialTrends" ("Category");
CREATE INDEX IF NOT EXISTS "IX_SocialTrends_Hashtag" ON "SocialTrends" ("Hashtag");
CREATE INDEX IF NOT EXISTS "IX_SocialTrends_TiktokGrowth" ON "SocialTrends" ("TiktokGrowth" DESC);
CREATE INDEX IF NOT EXISTS "IX_SocialTrends_UpdatedAt" ON "SocialTrends" ("UpdatedAt" DESC);

COMMENT ON TABLE "SocialTrends" IS 'Social media trends from TikTok, Instagram, Pinterest';
COMMENT ON COLUMN "SocialTrends"."TiktokGrowth" IS 'Percentage growth in TikTok posts';
COMMENT ON COLUMN "SocialTrends"."AverageEngagement" IS 'Average engagement rate across platforms';


-- ============================================
-- 3. GLOBAL TREND SCORES TABLE
-- Maps local products to global trends with scoring
-- ============================================
CREATE TABLE IF NOT EXISTS "GlobalTrendScores" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "LocalProductId" INT NOT NULL, -- References Artikli.Id from main database
    "ProductName" TEXT NOT NULL,
    
    -- Individual scores (0-100)
    "EuTrendScore" FLOAT DEFAULT 0,
    "SocialTrendScore" FLOAT DEFAULT 0,
    "SimilarityScore" FLOAT DEFAULT 0,
    "ColorScore" FLOAT DEFAULT 0,
    "PriceScore" FLOAT DEFAULT 0,
    "SeasonScore" FLOAT DEFAULT 0,
    
    -- Final weighted score
    "FinalGlobalScore" FLOAT DEFAULT 0,
    
    -- Metadata
    "MatchedEuTrendId" UUID, -- Best matching EU trend
    "MatchedHashtags" TEXT[], -- Relevant social media hashtags
    "Recommendations" TEXT, -- AI-generated recommendations
    
    "UpdatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    
    UNIQUE("LocalProductId")
);

-- Indexes for GlobalTrendScores
CREATE INDEX IF NOT EXISTS "IX_GlobalTrendScores_LocalProductId" ON "GlobalTrendScores" ("LocalProductId");
CREATE INDEX IF NOT EXISTS "IX_GlobalTrendScores_FinalGlobalScore" ON "GlobalTrendScores" ("FinalGlobalScore" DESC);
CREATE INDEX IF NOT EXISTS "IX_GlobalTrendScores_EuTrendScore" ON "GlobalTrendScores" ("EuTrendScore" DESC);
CREATE INDEX IF NOT EXISTS "IX_GlobalTrendScores_SocialTrendScore" ON "GlobalTrendScores" ("SocialTrendScore" DESC);
CREATE INDEX IF NOT EXISTS "IX_GlobalTrendScores_UpdatedAt" ON "GlobalTrendScores" ("UpdatedAt" DESC);

COMMENT ON TABLE "GlobalTrendScores" IS 'Mapping of local products to global trends with AI-powered scoring';
COMMENT ON COLUMN "GlobalTrendScores"."FinalGlobalScore" IS 'Weighted average of all trend scores (0-100)';
COMMENT ON COLUMN "GlobalTrendScores"."Recommendations" IS 'AI-generated suggestions for improving trend alignment';


-- ============================================
-- 4. TREND HISTORY TABLE
-- Tracks historical trend scores for analysis
-- ============================================
CREATE TABLE IF NOT EXISTS "TrendHistory" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "LocalProductId" INT NOT NULL,
    "Date" DATE NOT NULL,
    "FinalGlobalScore" FLOAT NOT NULL,
    "EuTrendScore" FLOAT,
    "SocialTrendScore" FLOAT,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    
    UNIQUE("LocalProductId", "Date")
);

CREATE INDEX IF NOT EXISTS "IX_TrendHistory_LocalProductId" ON "TrendHistory" ("LocalProductId");
CREATE INDEX IF NOT EXISTS "IX_TrendHistory_Date" ON "TrendHistory" ("Date" DESC);

COMMENT ON TABLE "TrendHistory" IS 'Historical trend scores for tracking changes over time';


-- ============================================
-- 5. HELPER FUNCTIONS
-- ============================================

-- Function to calculate final trend score
CREATE OR REPLACE FUNCTION calculate_final_trend_score(
    eu_score FLOAT,
    social_score FLOAT,
    similarity_score FLOAT,
    color_score FLOAT,
    price_score FLOAT,
    season_score FLOAT
)
RETURNS FLOAT AS $$
BEGIN
    -- Weighted average:
    -- EU trends: 30%
    -- Social trends: 25%
    -- Similarity: 20%
    -- Color match: 10%
    -- Price competitiveness: 10%
    -- Season relevance: 5%
    RETURN (
        COALESCE(eu_score, 0) * 0.30 +
        COALESCE(social_score, 0) * 0.25 +
        COALESCE(similarity_score, 0) * 0.20 +
        COALESCE(color_score, 0) * 0.10 +
        COALESCE(price_score, 0) * 0.10 +
        COALESCE(season_score, 0) * 0.05
    );
END;
$$ LANGUAGE plpgsql;

-- Function to find similar EU trends for a product
CREATE OR REPLACE FUNCTION find_similar_eu_trends(
    query_embedding vector(512),
    match_threshold float DEFAULT 0.7,
    match_count int DEFAULT 5
)
RETURNS TABLE (
    id uuid,
    product_name text,
    brand text,
    category text,
    similarity float
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT 
        et."Id",
        et."ProductName",
        et."Brand",
        et."Category",
        1 - (et."Embedding" <=> query_embedding) AS similarity
    FROM "EuTrends" et
    WHERE et."Embedding" IS NOT NULL
        AND 1 - (et."Embedding" <=> query_embedding) > match_threshold
    ORDER BY et."Embedding" <=> query_embedding
    LIMIT match_count;
END;
$$;

-- Function to get top trending categories
CREATE OR REPLACE FUNCTION get_top_trending_categories(limit_count int DEFAULT 10)
RETURNS TABLE (
    category text,
    avg_rank float,
    product_count bigint,
    avg_growth float
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT 
        et."Category",
        AVG(et."Rank")::float AS avg_rank,
        COUNT(*)::bigint AS product_count,
        COALESCE(AVG(st."TiktokGrowth"), 0)::float AS avg_growth
    FROM "EuTrends" et
    LEFT JOIN "SocialTrends" st ON et."Category" = st."Category"
    WHERE et."Category" IS NOT NULL
    GROUP BY et."Category"
    ORDER BY avg_rank ASC, avg_growth DESC
    LIMIT limit_count;
END;
$$;

-- Function to archive old trend data
CREATE OR REPLACE FUNCTION archive_old_trends()
RETURNS void AS $$
BEGIN
    -- Delete EU trends older than 90 days
    DELETE FROM "EuTrends"
    WHERE "UpdatedAt" < NOW() - INTERVAL '90 days';
    
    -- Delete social trends older than 90 days
    DELETE FROM "SocialTrends"
    WHERE "UpdatedAt" < NOW() - INTERVAL '90 days';
    
    -- Delete trend history older than 1 year
    DELETE FROM "TrendHistory"
    WHERE "Date" < CURRENT_DATE - INTERVAL '1 year';
    
    RAISE NOTICE 'Archived old trend data';
END;
$$ LANGUAGE plpgsql;


-- ============================================
-- 6. SAMPLE DATA (for testing)
-- ============================================

-- Insert sample EU trends
INSERT INTO "EuTrends" ("ProductName", "Brand", "Category", "Color", "Rank", "Price", "Season")
VALUES 
    ('Air Max 90', 'Nike', 'Patike', 'White/Red', 1, 120.00, 'Prolece-Leto'),
    ('Superstar', 'Adidas', 'Patike', 'White', 2, 90.00, 'Cela godina'),
    ('Chuck Taylor', 'Converse', 'Patike', 'Black', 3, 65.00, 'Cela godina'),
    ('Yeezy Boost', 'Adidas', 'Patike', 'Beige', 4, 220.00, 'Jesen-Zima'),
    ('Air Jordan 1', 'Nike', 'Patike', 'Black/Red', 5, 170.00, 'Cela godina')
ON CONFLICT DO NOTHING;

-- Insert sample social trends
INSERT INTO "SocialTrends" ("Category", "Hashtag", "PostsThisMonth", "PostsLastMonth", "TiktokGrowth")
VALUES 
    ('Patike', '#sneakerhead', 150000, 120000, 25.0),
    ('Patike', '#airmax', 80000, 60000, 33.3),
    ('Patike', '#yeezy', 100000, 90000, 11.1),
    ('Patike', '#jordans', 120000, 110000, 9.1),
    ('Sandale', '#summershoes', 50000, 30000, 66.7)
ON CONFLICT ("Category", "Hashtag") DO UPDATE SET
    "PostsThisMonth" = EXCLUDED."PostsThisMonth",
    "PostsLastMonth" = EXCLUDED."PostsLastMonth",
    "TiktokGrowth" = EXCLUDED."TiktokGrowth",
    "UpdatedAt" = NOW();


-- ============================================
-- 7. VERIFICATION
-- ============================================

-- Verify tables were created
SELECT 
    'EuTrends' AS table_name,
    COUNT(*) AS row_count
FROM "EuTrends"
UNION ALL
SELECT 
    'SocialTrends',
    COUNT(*)
FROM "SocialTrends"
UNION ALL
SELECT 
    'GlobalTrendScores',
    COUNT(*)
FROM "GlobalTrendScores"
UNION ALL
SELECT 
    'TrendHistory',
    COUNT(*)
FROM "TrendHistory";

-- Show pgvector version
SELECT * FROM pg_extension WHERE extname = 'vector';

-- Test similarity search function
SELECT * FROM get_top_trending_categories(5);
