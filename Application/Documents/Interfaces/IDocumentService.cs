using Application.Documents.Models;

namespace Application.Documents.Interfaces;

public interface IDocumentService
{
    Task<DocumentGenerateResult> GenerateAsync(
        DocumentGenerationRequest request,
        DocumentExecutionContext executionContext,
        CancellationToken ct = default);

    Task<DocumentBatchResult> EnqueueBatchAsync(
        DocumentBatchRequest request,
        DocumentExecutionContext executionContext,
        CancellationToken ct = default);

    Task<DocumentStatusResult?> GetStatusAsync(
        Guid documentId,
        DocumentExecutionContext executionContext,
        CancellationToken ct = default);

    Task<DocumentStreamResult?> OpenDownloadAsync(
        Guid documentId,
        DocumentExecutionContext executionContext,
        string? signedToken,
        CancellationToken ct = default);

    Task<string?> GetPrintHtmlAsync(
        Guid documentId,
        DocumentExecutionContext executionContext,
        string? signedToken,
        CancellationToken ct = default);

    Task ProcessQueuedDocumentAsync(Guid documentId, CancellationToken ct = default);
}
