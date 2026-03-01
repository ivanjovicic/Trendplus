-- Create view with quoted column names expected by EF
CREATE OR REPLACE VIEW "ProductsDim" AS
SELECT
  productkey AS "ProductKey",
  productid AS "ProductId",
  plu AS "PLU",
  productname AS "ProductName",
  category AS "Category",
  subcategory AS "SubCategory",
  brand AS "Brand",
  footweartypeid AS "FootwearTypeId",
  supplierid AS "SupplierId",
  seasonid AS "SeasonId",
  purchaseprice AS "PurchasePrice",
  purchasepricersd AS "PurchasePriceRsd",
  firstsaleprice AS "FirstSalePrice",
  saleprice AS "SalePrice",
  isactive AS "IsActive",
  timestamp AS "Timestamp",
  kolicina AS "Kolicina",
  minimalnakolicina AS "MinimalnaKolicina",
  boja AS "Boja",
  velicina AS "Velicina",
  materijal AS "Materijal",
  dataorigin AS "DataOrigin"
FROM productsdim;
