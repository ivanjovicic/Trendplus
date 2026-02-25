-- Add ImagePath column to Artikli table for product images

DO $$ 
BEGIN 
    -- Add ImagePath column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Artikli' AND column_name = 'ImagePath'
    ) THEN
        ALTER TABLE "Artikli" ADD COLUMN "ImagePath" VARCHAR(500);
        
        -- Create index for faster lookups
        CREATE INDEX IF NOT EXISTS "IX_Artikli_ImagePath" ON "Artikli" ("ImagePath");
        
        RAISE NOTICE 'Added ImagePath column to Artikli table';
    ELSE
        RAISE NOTICE 'ImagePath column already exists';
    END IF;
END $$;

-- Verify
SELECT column_name, data_type, character_maximum_length 
FROM information_schema.columns 
WHERE table_name = 'Artikli' AND column_name = 'ImagePath';
