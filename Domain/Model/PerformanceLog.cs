using System;

namespace Domain.Model
{
    public class PerformanceLog
    {
        public long Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string RequestType { get; set; } = string.Empty;
        public string RequestName { get; set; } = string.Empty;
        public long DurationMs { get; set; }
        public string? RequestData { get; set; }
        public string? ResponseData { get; set; }
        public string? ExceptionMessage { get; set; }
        public bool IsSuccess { get; set; }
    }
}
