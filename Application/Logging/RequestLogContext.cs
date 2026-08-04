using System.Collections.Generic;
using System.Threading;

namespace Application.Logging;

public sealed class RequestLogContext
{
    private const int CapturedSqlExecutionLimit = 20;
    private static readonly AsyncLocal<RequestLogContext?> CurrentContext = new();
    private readonly List<CapturedSqlExecution> _capturedSqlExecutions = new();

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

    public IReadOnlyList<CapturedSqlExecution> CapturedSqlExecutions => _capturedSqlExecutions.ToArray();

    public void CaptureSqlExecution(CapturedSqlExecution execution)
    {
        if (!ShouldCaptureSql || execution is null)
        {
            return;
        }

        if (_capturedSqlExecutions.Count >= CapturedSqlExecutionLimit)
        {
            return;
        }

        _capturedSqlExecutions.Add(execution);
    }
}
