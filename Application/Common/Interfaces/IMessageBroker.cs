using System.Threading;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface IMessageBroker
    {
        Task PublishAsync<T>(string eventType, T payload, string? routingKey = null, CancellationToken ct = default);
        bool IsEnabled { get; }
    }
}
