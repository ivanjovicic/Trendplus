using Domain.Model.Documents;

namespace Application.Documents.Interfaces;

public interface IDocumentAccessControlService
{
    void EnsureCanGenerate(Application.Documents.Models.DocumentExecutionContext executionContext);
    void EnsureCanAccess(DocumentRecord document, Application.Documents.Models.DocumentExecutionContext executionContext);
    bool CanBypassOwnership(Application.Documents.Models.DocumentExecutionContext executionContext);
}
