using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Model;

namespace Application.Common.Interfaces
{
    public interface IErrorStore
    {
        Task<IReadOnlyList<ErrorRecord>> GetAllAsync(
            string? level = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchText = null,
            CancellationToken cancellationToken = default);

        Task<int> GetCountAsync(
            string? level = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchText = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ErrorRecord>> GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? level = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchText = null,
            CancellationToken cancellationToken = default);

        Task<ErrorRecord?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task SaveAsync(
            ErrorRecord error,
            CancellationToken cancellationToken = default);
    }
}
