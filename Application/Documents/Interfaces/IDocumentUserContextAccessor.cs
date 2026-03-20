using Application.Documents.Models;

namespace Application.Documents.Interfaces;

public interface IDocumentUserContextAccessor
{
    DocumentExecutionContext GetCurrent();
}
