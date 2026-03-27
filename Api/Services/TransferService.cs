using System;
using System.Threading;
using System.Threading.Tasks;
using Api.Dtos;

namespace Api.Services
{
    public interface ITransferService
    {
        Task<TransferResponse> CreateAsync(TransferCreateRequest req, string userId, CancellationToken ct = default);
        Task<TransferResponse?> GetAsync(long id, CancellationToken ct = default);
    }

    public class TransferService : ITransferService
    {
        public Task<TransferResponse> CreateAsync(TransferCreateRequest req, string userId, CancellationToken ct = default)
        {
            // TODO: implement validation, reservation and persistence
            throw new NotImplementedException();
        }

        public Task<TransferResponse?> GetAsync(long id, CancellationToken ct = default)
        {
            // TODO: project lightweight TransferResponse
            throw new NotImplementedException();
        }
    }
}
