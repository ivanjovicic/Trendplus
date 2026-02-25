-- ============================================
-- Add pgvector extension and ProductImages table
-- ============================================

-- Enable pgvector extension for vector similarity search
CREATE EXTENSION IF NOT EXISTS vector;

-- Create ProductImages table
CREATE TABLE IF NOT EXISTS "ProductImages" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProductId" INTEGER NOT NULL,
    "FileName" VARCHAR(500) NOT NULL,
    "Embedding" vector(512), -- 512-dimensional embedding vector
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "IsPrimary" BOOLEAN NOT NULL DEFAULT FALSE,
    
    -- Foreign key to Artikli table
    CONSTRAINT "FK_ProductImages_Artikli" 
        FOREIGN KEY ("ProductId") 
        REFERENCES "Artikli"("Id") 
        ON DELETE CASCADE
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS "IX_ProductImages_ProductId" 
    ON "ProductImages" ("ProductId");

CREATE INDEX IF NOT EXISTS "IX_ProductImages_CreatedAt" 
    ON "ProductImages" ("CreatedAt" DESC);

CREATE INDEX IF NOT EXISTS "IX_ProductImages_IsPrimary" 
    ON "ProductImages" ("ProductId", "IsPrimary") 
    WHERE "IsPrimary" = TRUE;

-- HNSW index for fast vector similarity search
-- This enables sub-linear time similarity searches
CREATE INDEX IF NOT EXISTS "IX_ProductImages_Embedding_HNSW" 
    ON "ProductImages" 
    USING hnsw ("Embedding" vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);

-- Alternative: IVFFlat index (faster build, slower search)
-- Uncomment if you prefer IVFFlat over HNSW
-- CREATE INDEX IF NOT EXISTS "IX_ProductImages_Embedding_IVFFlat" 
--     ON "ProductImages" 
--     USING ivfflat ("Embedding" vector_cosine_ops)
--     WITH (lists = 100);

-- Add ImagePath column to Artikli if it doesn't exist
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Artikli' AND column_name = 'ImagePath'
    ) THEN
        ALTER TABLE "Artikli" ADD COLUMN "ImagePath" VARCHAR(500);
        CREATE INDEX IF NOT EXISTS "IX_Artikli_ImagePath" ON "Artikli" ("ImagePath");
    END IF;
END $$;

-- Function to find similar images by vector
CREATE OR REPLACE FUNCTION find_similar_products(
    query_embedding vector(512),
    match_threshold float DEFAULT 0.8,
    match_count int DEFAULT 10
)
RETURNS TABLE (
    product_id int,
    product_name text,
    image_file_name text,
    similarity float
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT 
        pi."ProductId",
        a."Naziv",
        pi."FileName",
        1 - (pi."Embedding" <=> query_embedding) AS similarity
    FROM "ProductImages" pi
    JOIN "Artikli" a ON pi."ProductId" = a."Id"
    WHERE pi."Embedding" IS NOT NULL
        AND 1 - (pi."Embedding" <=> query_embedding) > match_threshold
    ORDER BY pi."Embedding" <=> query_embedding
    LIMIT match_count;
END;
$$;

-- Comments
COMMENT ON TABLE "ProductImages" IS 'Stores product images with AI embeddings for similarity search';
COMMENT ON COLUMN "ProductImages"."Embedding" IS '512-dimensional vector embedding from CLIP or similar model';
COMMENT ON COLUMN "ProductImages"."IsPrimary" IS 'Indicates if this is the primary/featured image for the product';

-- Grant permissions (adjust user as needed)
-- GRANT SELECT, INSERT, UPDATE, DELETE ON "ProductImages" TO your_app_user;

-- Verification query
SELECT 
    'ProductImages table created' AS status,
    COUNT(*) AS row_count
FROM "ProductImages";

-- Show pgvector version
SELECT * FROM pg_extension WHERE extname = 'vector';
