using System;

namespace Domain.Model;

/// <summary>
/// Line-level supplier return fact stored in the analytics DB.
/// This decouples supplier-quality analytics from operational povracaj tables.
/// </summary>
public class ReturnFact
{
    public long Id { get; set; }

    /// <summary>Source line Id from trendplus povracaj_stavke.</summary>
    public int SourceLineId { get; set; }

    /// <summary>Source header Id from trendplus povracaj_zaglavlje.</summary>
    public int ReturnId { get; set; }

    public int ProductId { get; set; }
    public int SupplierId { get; set; }
    public int Qty { get; set; }

    public decimal UnitCost { get; set; }
    public decimal LineAmount { get; set; }

    public DateTime ReturnTimestampUtc { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? HeaderReason { get; set; }
    public string? LineReason { get; set; }
    public string? ItemCondition { get; set; }
    public string? BrojZapisnika { get; set; }

    public string DataOrigin { get; set; } = "existing";
}
