namespace Domain.Model
{
    /// <summary>
    /// Identity of the source receipt line (prodaja_stavke row) behind a SalesLineFacts row.
    /// A receipt may carry the same product on several lines, so (SaleId, ProductId) is not a
    /// line key. Access-imported lines use their import lineage (source_table_key, source_row_id),
    /// which is unique in prodaja_stavke (ux_prodaja_stavke_source_row). Lines created in
    /// Trendplus itself (POS) have no lineage and use the prodaja_stavke primary key in their own
    /// namespace, so the two id spaces can never collide.
    /// </summary>
    public static class SalesLineSourceIdentity
    {
        public const string TrendplusLineNamespace = "trendplus.prodaja_stavke";

        public static (string? SourceTableKey, long? SourceLineId) Resolve(
            int prodajaStavkaId,
            string? sourceTableKey,
            long? sourceRowId)
        {
            if (sourceRowId.HasValue && !string.IsNullOrWhiteSpace(sourceTableKey))
                return (sourceTableKey, sourceRowId.Value);

            if (prodajaStavkaId > 0)
                return (TrendplusLineNamespace, prodajaStavkaId);

            return (null, null);
        }
    }
}
