using System.Threading;

namespace Application.Logging;

public sealed class RequestLogContext
{
    private static readonly AsyncLocal<RequestLogContext?> CurrentContext = new();

    public static RequestLogContext Current
    {
        get
        {
            if (CurrentContext.Value == null)
            {
                CurrentContext.Value = new RequestLogContext();
            }

            return CurrentContext.Value;
        }
        set => CurrentContext.Value = value;
    }

    public string? RequestId { get; set; }
    public string? TraceId { get; set; }
    public bool ShouldCaptureSql { get; set; }
    public int? MaxQueryLength { get; set; }
}
