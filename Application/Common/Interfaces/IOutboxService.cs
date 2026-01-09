using System.Threading;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface IOutboxService
    {
        Task PublishAsync<T>(string eventType, T payload, string? correlationId = null, CancellationToken ct = default);
    }
}
