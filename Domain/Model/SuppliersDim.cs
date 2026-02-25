namespace Domain.Model;

/// <summary>
/// Supplier dimension in the analytics DB — denormalized copy of Dobavljaci.
/// Populated by SyncWorker; used for analytics queries without cross-DB joins.
/// </summary>
public class SuppliersDim
{
    public int SupplierKey { get; set; }    // identity PK
    public int SupplierId { get; set; }     // matches Dobavljaci.Id

    public string Naziv { get; set; } = string.Empty;
    public string? Adresa { get; set; }
    public string? Telefon { get; set; }
    public string? Napomena { get; set; }

    public string DataOrigin { get; set; } = "existing";
    public DateTime UpdatedAt { get; set; }
}
