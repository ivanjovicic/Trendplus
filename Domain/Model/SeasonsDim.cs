namespace Domain.Model;

/// <summary>
/// Season dimension in the analytics DB — denormalized copy of Sezone.
/// Populated by SyncWorker; used for analytics queries without cross-DB joins.
/// </summary>
public class SeasonsDim
{
    public int SeasonKey { get; set; }      // identity PK
    public int SeasonId { get; set; }       // matches Sezone.Id

    public string Naziv { get; set; } = string.Empty;
    public DateTime DatumOd { get; set; }
    public DateTime DatumDo { get; set; }

    public string DataOrigin { get; set; } = "existing";
    public DateTime UpdatedAt { get; set; }
}
