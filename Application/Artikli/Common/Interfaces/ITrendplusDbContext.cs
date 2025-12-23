using Domain.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace Application.Artikli.Common.Interfaces
{
    public interface ITrendplusDbContext
    {
        DbSet<Domain.Model.Artikli> Artikli { get; }
        DbSet<Domain.Model.TipObuce> TipoviObuce { get; }
        DbSet<Domain.Model.Dobavljac> Dobavljaci { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<int> ExecuteStoredProcedureWithOutputAsync(string sql, SqlParameter[] parameters, CancellationToken cancellationToken = default);

    }
}
