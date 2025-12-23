using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Model;

namespace Application.Common.Interfaces
{
    public interface IErrorStore
    {
        Task LogAsync(ErrorRecord record);
        Task<IReadOnlyList<ErrorRecord>> GetAllAsync();
    }
}
