DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='productkey') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='ProductKey') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN productkey TO "ProductKey"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='productid') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='ProductId') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN productid TO "ProductId"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='productname') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='ProductName') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN productname TO "ProductName"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='category') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='Category') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN category TO "Category"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='subcategory') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='SubCategory') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN subcategory TO "SubCategory"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='brand') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='Brand') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN brand TO "Brand"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='footweartypeid') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='FootwearTypeId') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN footweartypeid TO "FootwearTypeId"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='supplierid') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='SupplierId') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN supplierid TO "SupplierId"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='seasonid') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='SeasonId') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN seasonid TO "SeasonId"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='purchaseprice') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='PurchasePrice') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN purchaseprice TO "PurchasePrice"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='purchasepricersd') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='PurchasePriceRsd') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN purchasepricersd TO "PurchasePriceRsd"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='firstsaleprice') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='FirstSalePrice') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN firstsaleprice TO "FirstSalePrice"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='saleprice') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='SalePrice') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN saleprice TO "SalePrice"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='isactive') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='IsActive') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN isactive TO "IsActive"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='timestamp') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='Timestamp') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN timestamp TO "Timestamp"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='kolicina') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='Kolicina') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN kolicina TO "Kolicina"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='boja') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='Boja') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN boja TO "Boja"';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='velicina') AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='productsdim' AND column_name='Velicina') THEN
        EXECUTE 'ALTER TABLE "ProductsDim" RENAME COLUMN velicina TO "Velicina"';
    END IF;
END $$;
