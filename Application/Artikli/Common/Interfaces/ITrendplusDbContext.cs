using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Application.Artikli.Common.Interfaces
{
    public interface ITrendplusDbContext
    {
        DbSet<Domain.Model.Artikli> Artikli { get; }
        DbSet<Domain.Model.TipObuce> TipoviObuce { get; }
        DbSet<Dobavljac> Dobavljaci { get; }

        DatabaseFacade Database { get; }

        /// <summary>
        /// Vraća sirovu DbConnection (za Npgsql komande).
        /// </summary>
        DbConnection GetDbConnection();

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
