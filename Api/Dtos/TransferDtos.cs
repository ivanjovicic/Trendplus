using System;
using System.Collections.Generic;

namespace Api.Dtos
{
    public record TransferItemDto(
        long SkuId,
        string? SkuCode,
        string? SkuName,
        decimal Quantity,
        decimal ReservedQuantity,
        decimal ProcessedQuantity,
        decimal? AvailableQuantity,
        string? Unit);

    public class TransferLineInputDto
    {
        public long SkuId { get; set; }
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
    }

    public class TransferCreateRequest
    {
        public long SourceId { get; set; }
        public long DestinationId { get; set; }
        public string SourceType { get; set; } = "store";
        public string DestinationType { get; set; } = "store";
        public bool Reserve { get; set; }
        public string? Notes { get; set; }
        public List<TransferLineInputDto> Items { get; set; } = new();
    }

    public class TransferUpdateRequest
    {
        public bool Reserve { get; set; }
        public string? Notes { get; set; }
        public List<TransferLineInputDto> Items { get; set; } = new();
    }

    public class TransferResponse
    {
        public long Id { get; set; }
        public string Status { get; set; } = "draft";
        public long SourceId { get; set; }
        public long DestinationId { get; set; }
        public bool Reserve { get; set; }
        public string? Notes { get; set; }
        public List<TransferItemDto> Items { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? ConfirmedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public DateTimeOffset? CancelledAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public decimal TotalQuantity { get; set; }
        public int LineCount { get; set; }
    }

    public class TransferListItemProjection
    {
        public long Id { get; set; }
        public string Status { get; set; } = "draft";
        public long SourceId { get; set; }
        public long DestinationId { get; set; }
        public bool Reserve { get; set; }
        public string? Notes { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalQuantity { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
    }

    public class TransferListResponse
    {
        public List<TransferListItemProjection> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
