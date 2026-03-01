CREATE TABLE IF NOT EXISTS "ProductsDim" (
  "ProductKey" SERIAL PRIMARY KEY,
  "ProductId" INTEGER NOT NULL,
  "PLU" TEXT,
  "ProductName" TEXT NOT NULL,
  "Category" TEXT NOT NULL,
  "SubCategory" TEXT NOT NULL,
  "Brand" TEXT NOT NULL,
  "FootwearTypeId" INTEGER,
  "SupplierId" INTEGER,
  "SeasonId" INTEGER,
  "PurchasePrice" NUMERIC,
  "PurchasePriceRsd" NUMERIC,
  "FirstSalePrice" NUMERIC,
  "SalePrice" NUMERIC,
  "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
  "Timestamp" TIMESTAMP WITH TIME ZONE NOT NULL,
  "Kolicina" INTEGER,
  "MinimalnaKolicina" INTEGER,
  "Boja" TEXT,
  "Velicina" TEXT,
  "Materijal" TEXT,
  "DataOrigin" VARCHAR(32) NOT NULL DEFAULT 'existing'
);

CREATE INDEX IF NOT EXISTS "IX_ProductsDim_ProductId" ON "ProductsDim" ("ProductId");
CREATE INDEX IF NOT EXISTS "IX_ProductsDim_Timestamp" ON "ProductsDim" ("Timestamp");
CREATE INDEX IF NOT EXISTS "IX_ProductsDim_DataOrigin" ON "ProductsDim" ("DataOrigin");