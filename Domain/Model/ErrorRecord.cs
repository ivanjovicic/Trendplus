using System;

namespace Domain.Model
{
    public sealed class ErrorRecord
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ExceptionType { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public string? Path { get; set; }
        public string? UserName { get; set; }
        public string? ClientApp { get; set; }
    }
}
