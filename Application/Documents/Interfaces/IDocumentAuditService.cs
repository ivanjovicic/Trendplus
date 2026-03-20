using Application.Documents.Models;

namespace Application.Documents.Interfaces;

public interface IDocumentAuditService
{
    Task WriteAsync(
        Guid documentId,
        string action,
        DocumentExecutionContext executionContext,
        object? details,
        CancellationToken ct = default);
}
