namespace Application.Documents.Interfaces;

public interface IDocumentDownloadTokenService
{
    string Create(Guid documentId, DateTime expiresAtUtc);
    bool TryValidate(Guid documentId, string? token);
}
