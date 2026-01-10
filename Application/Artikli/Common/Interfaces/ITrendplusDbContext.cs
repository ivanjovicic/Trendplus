using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Domain.Model;
using Domain.Model.Prodaja;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Application.Artikli.Common.Interfaces
{
    public interface ITrendplusDbContext
    {
        DbSet<Domain.Model.Artikli> Artikli { get; }
        DbSet<Domain.Model.TipObuce> TipoviObuce { get; }
        DbSet<Dobavljac> Dobavljaci { get; }
        DbSet<Domain.Model.CreatedIdDto> CreatedIds { get; }
        DbSet<Domain.Model.DnevnikPromena> DnevnikPromena { get; }
        DbSet<Domain.Model.Sezona> Sezone { get; }
        DbSet<Domain.Model.OutboxMessage> OutboxMessages { get; }
        DbSet<ProdajaZaglavlje> ProdajaZaglavlja { get; }
        DbSet<ProdajaStavka> ProdajaStavke { get; }

        DatabaseFacade Database { get; }

        /// <summary>
        /// Vraća sirovu DbConnection (za Npgsql komande).
        /// </summary>
        DbConnection GetDbConnection();

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
