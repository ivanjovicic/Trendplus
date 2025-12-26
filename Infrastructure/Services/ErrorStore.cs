using Application.Common.Interfaces;
using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

public class ErrorStore : IErrorStore
{
    private readonly TrendplusDbContext _db;

    public ErrorStore(TrendplusDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ErrorRecord>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.ErrorRecords
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(
        ErrorRecord error,
        CancellationToken cancellationToken = default)
    {
        _db.ErrorRecords.Add(error);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
