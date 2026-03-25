namespace Api.Models
{
    public sealed class AccessImportCoverageMetric
    {
        public int SourceRows { get; set; }
        public int AcceptedRows { get; set; }
        public int SkippedRows { get; set; }
        public int TargetWrites { get; set; }
        public int MergedRows { get; set; }
        public int ExpandedTargetRows { get; set; }
        public double CoveragePercent { get; set; }
        public string TransformationType { get; set; } = "direct";
    }

    public sealed class AccessImportFieldMappingPreview
    {
        public string TargetField { get; set; } = string.Empty;
        public string? SourceColumn { get; set; }
        public string Status { get; set; } = "missing";
    }

    public sealed class AccessImportTablePreview
    {
        public string Key { get; set; } = string.Empty;
        public string? TableName { get; set; }
        public int RowCount { get; set; }
        public string RowCountMode { get; set; } = "unknown";
        public string MatchStrategy { get; set; } = "none";
        public List<string> AccessColumns { get; set; } = new();
        public List<AccessImportFieldMappingPreview> FieldMappings { get; set; } = new();
        public int MatchedMappings { get; set; }
        public int TotalMappings { get; set; }
        public double MappingCoveragePercent { get; set; }
        public List<string> RequiredFieldsMissing { get; set; } = new();
        public List<string> UnmappedAccessColumns { get; set; } = new();
        public bool Found => !string.IsNullOrWhiteSpace(TableName);
        public bool HasRows => RowCount > 0;
    }

    public sealed class AccessImportPreviewResponse
    {
        public bool CanImport { get; set; }
        public string SourceFileName { get; set; } = string.Empty;
        public List<AccessImportTablePreview> Tables { get; set; } = new();
        public List<string> AvailableTables { get; set; } = new();
        public int TotalAccessTables { get; set; }
        public int AccessTablesWithRows { get; set; }
        public int MappedAccessTables { get; set; }
        public int MappedAccessTablesWithRows { get; set; }
        public int TotalAccessRows { get; set; }
        public int MappedAccessRows { get; set; }
        public double RowCoveragePercent { get; set; }
        public List<string> UnmappedAccessTablesWithRows { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public sealed class AccessImportRunResponse
    {
        public long BatchId { get; set; }
        public string Status { get; set; } = "completed";
        public string SourceFileName { get; set; } = string.Empty;
        public bool IncludeAnalytics { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }

        public int TipoviInserted { get; set; }
        public int TipoviUpdated { get; set; }
        public int DobavljaciInserted { get; set; }
        public int DobavljaciUpdated { get; set; }
        public int SezoneInserted { get; set; }
        public int SezoneUpdated { get; set; }
        public int ArtikliInserted { get; set; }
        public int ArtikliUpdated { get; set; }
        public int ProdajaInserted { get; set; }
        public int ProdajaUpdated { get; set; }
        public int ProdajaStavkeInserted { get; set; }
        public int ProdajaStavkeUpdated { get; set; }
        public int DnevnikInserted { get; set; }
        public int DnevnikUpdated { get; set; }
        public int PovracajInserted { get; set; }
        public int PovracajUpdated { get; set; }
        public int PovracajStavkeInserted { get; set; }
        public int PovracajStavkeUpdated { get; set; }

        // New movement types (all go into DnevnikPromena)
        public int NivelacijeInserted { get; set; }
        public int UnosRobeInserted { get; set; }
        public int PovratnicaInserted { get; set; }   // customer / kupac returns
        public int PrenosRobeInserted { get; set; }   // inter-store transfers (each transfer = 2 entries)
        public int ObjekatInserted { get; set; }
        public int ObjekatUpdated { get; set; }

        public int ProductsDimInserted { get; set; }
        public int ProductsDimUpdated { get; set; }
        public int SalesFactsInserted { get; set; }
        public int SalesFactsUpdated { get; set; }
        public int SalesLineFactsInserted { get; set; }
        public int StoresInserted { get; set; }
        public int StoresUpdated { get; set; }
        public Dictionary<string, int> SourceRowsByTable { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> ImportedRowsByTable { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, AccessImportCoverageMetric> CoverageByTable { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Warnings { get; set; } = new();
    }

    public sealed class AccessImportBatchDto
    {
        public long Id { get; set; }
        public string SourceSystem { get; set; } = string.Empty;
        public string SourceFileName { get; set; } = string.Empty;
        public DateTime StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? LastHeartbeatUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CurrentStep { get; set; }
        public string? CurrentTable { get; set; }
        public string? SummaryJson { get; set; }
        public string? ErrorMessage { get; set; }

        // Enhanced (migration 015)
        public int? DurationSeconds { get; set; }
        public int TotalImported { get; set; }
        public int TotalUpdated { get; set; }
        public int TotalErrors { get; set; }
        public string DataOrigin { get; set; } = "access";
    }

    public sealed class AccessImportLogDto
    {
        public long Id { get; set; }
        public long BatchId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public int RowIndex { get; set; }
        public string Severity { get; set; } = "info";
        public string Message { get; set; } = string.Empty;
        public string? SourceRowJson { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class BatchDetailDto
    {
        public AccessImportBatchDto Batch { get; set; } = null!;
        public List<AccessImportLogDto> Logs { get; set; } = new();
        public Dictionary<string, int> LogCountBySeverity { get; set; } = new();
        public Dictionary<string, int> LogCountByTable { get; set; } = new();
    }

    public sealed class DeleteBatchResult
    {
        public bool Found { get; set; }
        public long BatchId { get; set; }
        public bool IncludeAnalytics { get; set; } = true;
        public int ArtikliDeleted { get; set; }
        public int SezoneDeleted { get; set; }
        public int TipoviDeleted { get; set; }
        public int DobavljaciDeleted { get; set; }
        public int ProdajaDeleted { get; set; }
        public int StavkeDeleted { get; set; }
        public int ProductsDimDeleted { get; set; }
        public int SalesFactsDeleted { get; set; }
        public int SalesLineFactsDeleted { get; set; }
        public int InventoryMovementsDeleted { get; set; }
        public int SuppliersDimDeleted { get; set; }
        public int SeasonsDimDeleted { get; set; }
        public int FootwearTypesDimDeleted { get; set; }
        public int StoresDimDeleted { get; set; }
        public bool CacheInvalidated { get; set; }
        public int DnevnikDeleted { get; set; }
        public int PovracajDeleted { get; set; }
        public int PovracajStavkeDeleted { get; set; }
    }
}
