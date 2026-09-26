namespace Application.Analytics;

public static class SupplierSalesProvenance
{
    public const string SourceFamily = "live_relational_sales_facts";
    public const string SourceLabel = "Živi podaci prodaje, artikala i nivelacija";
    public const string SourceTables = "ProdajaZaglavlja, ProdajaStavke, Artikli i DnevnikPromena";
    public const string ObservedPopulation = "Sve prodajne stavke u filtriranom periodu, objektu i opsegu podataka";
    public const string CostPolicy = "Istorijski trošak prodajne stavke; snapshot trošak; fallback nabavna cena artikla; nepokriveni promet ostaje izdvojen";
    public const string PrePostPolicy = "Uporediva kohorta artikala sa prodajom pre i posle prve nivelacije; posmatrani pre/post ostaje odvojen";
}
