using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Model;

namespace Application.Common.Interfaces
{
    public interface IErrorStore
    {
        Task<IReadOnlyList<ErrorRecord>> GetAllAsync(CancellationToken cancellationToken = default);
        Task SaveAsync(
           ErrorRecord error,
           CancellationToken cancellationToken = default);
    }
}
