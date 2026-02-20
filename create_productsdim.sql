CREATE TABLE IF NOT EXISTS "ProductsDim" (
  "ProductKey" SERIAL PRIMARY KEY,
  "ProductId" INTEGER NOT NULL,
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
  "Boja" TEXT,
  "Velicina" TEXT
);

CREATE INDEX IF NOT EXISTS "IX_ProductsDim_ProductId" ON "ProductsDim" ("ProductId");
CREATE INDEX IF NOT EXISTS "IX_ProductsDim_Timestamp" ON "ProductsDim" ("Timestamp");