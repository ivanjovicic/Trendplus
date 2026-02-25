namespace Domain.Model;

/// <summary>
/// Footwear type dimension in the analytics DB — denormalized copy of TipoviObuce.
/// Populated by SyncWorker; used for analytics queries without cross-DB joins.
/// </summary>
public class FootwearTypesDim
{
    public int TypeKey { get; set; }        // identity PK
    public int TypeId { get; set; }         // matches TipoviObuce.Id

    public string Naziv { get; set; } = string.Empty;

    public string DataOrigin { get; set; } = "existing";
    public DateTime UpdatedAt { get; set; }
}
