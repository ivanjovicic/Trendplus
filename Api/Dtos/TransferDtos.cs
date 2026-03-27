using System;
using System.Collections.Generic;

namespace Api.Dtos
{
    public record TransferItemDto(long SkuId, string? SkuCode, decimal Quantity, string? Unit);

    public class TransferCreateRequest
    {
        public long SourceId { get; set; }
        public long DestinationId { get; set; }
        public string SourceType { get; set; } = "store";
        public string DestinationType { get; set; } = "store";
        public bool Reserve { get; set; }
        public string? Notes { get; set; }
        public List<TransferItemDto> Items { get; set; } = new();
    }

    public class TransferResponse
    {
        public long Id { get; set; }
        public string Status { get; set; } = "draft";
        public long SourceId { get; set; }
        public long DestinationId { get; set; }
        public bool Reserve { get; set; }
        public List<TransferItemDto> Items { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class TransferListItemProjection
    {
        public long Id { get; set; }
        public string Status { get; set; } = "draft";
        public long SourceId { get; set; }
        public long DestinationId { get; set; }
        public int ItemCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
