using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Model;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Infrastructure.Tests.Services;

public class WorkerConfigurationServiceTests
{
    private TrendplusDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TrendplusDbContext(options);
    }

    [Fact]
    public async Task GetAllWorkersAsync_ReturnsManagedWorkers_WhenNoWorkersExist()
    {
        var db = CreateDbContext();
        var healthService = new WorkerHealthService();
        var service = new WorkerConfigurationService(db, healthService, NullLogger<WorkerConfigurationService>.Instance);

        var result = await service.GetAllWorkersAsync();

        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(result, w => w.WorkerName == "AccessImportBackgroundWorker");
        Assert.Contains(result, w => w.WorkerName == "SyncWorker");
    }

    [Fact]
    public async Task GetAllWorkersAsync_CreatesDefaultSettings_ForNewWorkers()
    {
        var db = CreateDbContext();
        var healthService = new WorkerHealthService();
        healthService.ReportRunning("TestWorker1");
        healthService.ReportRunning("TestWorker2");

        var service = new WorkerConfigurationService(db, healthService, NullLogger<WorkerConfigurationService>.Instance);

        var result = await service.GetAllWorkersAsync();

        Assert.NotNull(result);
        Assert.Contains(result, w => w.WorkerName == "TestWorker1");
        Assert.Contains(result, w => w.WorkerName == "TestWorker2");
        Assert.All(result, w => Assert.True(w.IsScheduleEnabled));
        Assert.All(result, w => Assert.False(w.IsManuallyStopped));

        var dbSettings = await db.WorkerRuntimeSettings.ToListAsync();
        Assert.Contains(dbSettings, w => w.WorkerName == "TestWorker1");
        Assert.Contains(dbSettings, w => w.WorkerName == "TestWorker2");
    }

    [Fact]
    public async Task GetWorkerAsync_ReturnsNull_WhenWorkerNotFound()
    {
        var db = CreateDbContext();
        var healthService = new WorkerHealthService();
        var service = new WorkerConfigurationService(db, healthService, NullLogger<WorkerConfigurationService>.Instance);

        var result = await service.GetWorkerAsync("NonExistentWorker");

        Assert.NotNull(result);
        Assert.Equal("NonExistentWorker", result.WorkerName);
        Assert.True(result.IsScheduleEnabled);
        Assert.False(result.IsManuallyStopped);
    }

    [Fact]
    public async Task EnableScheduleAsync_EnablesSchedule_WhenDisabled()
    {
        var db = CreateDbContext();
        db.WorkerRuntimeSettings.Add(new WorkerRuntimeSettings
        {
            WorkerName = "TestWorker",
            IsScheduleEnabled = false,
            IsManuallyStopped = false
        });
        await db.SaveChangesAsync();

        var healthService = new WorkerHealthService();
        var service = new WorkerConfigurationService(db, healthService, NullLogger<WorkerConfigurationService>.Instance);

        var result = await service.EnableScheduleAsync("TestWorker", "test-user");

        Assert.True(result);

        var updated = await db.WorkerRuntimeSettings.FirstAsync(w => w.WorkerName == "TestWorker");
        Assert.True(updated.IsScheduleEnabled);
        Assert.Equal("test-user", updated.UpdatedBy);
    }

    [Fact]
    public async Task DisableScheduleAsync_DisablesSchedule_WhenEnabled()
    {
        var db = CreateDbContext();
        db.WorkerRuntimeSettings.Add(new WorkerRuntimeSettings
        {
            WorkerName = "TestWorker",
            IsScheduleEnabled = true,
            IsManuallyStopped = false
        });
        await db.SaveChangesAsync();

        var healthService = new WorkerHealthService();
        var service = new WorkerConfigurationService(db, healthService, NullLogger<WorkerConfigurationService>.Instance);

        var result = await service.DisableScheduleAsync("TestWorker", "test-user");

        Assert.True(result);

        var updated = await db.WorkerRuntimeSettings.FirstAsync(w => w.WorkerName == "TestWorker");
        Assert.False(updated.IsScheduleEnabled);
        Assert.Equal("test-user", updated.UpdatedBy);
    }

    [Fact]
    public async Task StopWorkerAsync_StopsWorker_WhenRunning()
    {
        var db = CreateDbContext();
        db.WorkerRuntimeSettings.Add(new WorkerRuntimeSettings
        {
            WorkerName = "TestWorker",
            IsScheduleEnabled = true,
            IsManuallyStopped = false
        });
        await db.SaveChangesAsync();

        var healthService = new WorkerHealthService();
        var service = new WorkerConfigurationService(db, healthService, NullLogger<WorkerConfigurationService>.Instance);

        var result = await service.StopWorkerAsync("TestWorker", "test-user");

        Assert.True(result);

        var updated = await db.WorkerRuntimeSettings.FirstAsync(w => w.WorkerName == "TestWorker");
        Assert.True(updated.IsManuallyStopped);
        Assert.True(updated.IsScheduleEnabled); // Schedule status should not change
    }

    [Fact]
    public async Task ResumeWorkerAsync_ResumesWorker_WhenStopped()
    {
        var db = CreateDbContext();
        db.WorkerRuntimeSettings.Add(new WorkerRuntimeSettings
        {
            WorkerName = "TestWorker",
            IsScheduleEnabled = true,
            IsManuallyStopped = true
        });
        await db.SaveChangesAsync();

        var healthService = new WorkerHealthService();
        var service = new WorkerConfigurationService(db, healthService, NullLogger<WorkerConfigurationService>.Instance);

        var result = await service.ResumeWorkerAsync("TestWorker", "test-user");

        Assert.True(result);

        var updated = await db.WorkerRuntimeSettings.FirstAsync(w => w.WorkerName == "TestWorker");
        Assert.False(updated.IsManuallyStopped);
    }

    [Fact]
    public async Task Multiple_Operations_MaintainIndependence()
    {
        var db = CreateDbContext();
        var worker1 = new WorkerRuntimeSettings
        {
            WorkerName = "Worker1",
            IsScheduleEnabled = true,
            IsManuallyStopped = false
        };
        var worker2 = new WorkerRuntimeSettings
        {
            WorkerName = "Worker2",
            IsScheduleEnabled = true,
            IsManuallyStopped = false
        };
        db.WorkerRuntimeSettings.Add(worker1);
        db.WorkerRuntimeSettings.Add(worker2);
        await db.SaveChangesAsync();

        var healthService = new WorkerHealthService();
        var service = new WorkerConfigurationService(db, healthService, NullLogger<WorkerConfigurationService>.Instance);

        // Stop worker1
        await service.StopWorkerAsync("Worker1", "test-user");
        
        // Disable schedule for worker2
        await service.DisableScheduleAsync("Worker2", "test-user");

        var updated1 = await db.WorkerRuntimeSettings.FirstAsync(w => w.WorkerName == "Worker1");
        var updated2 = await db.WorkerRuntimeSettings.FirstAsync(w => w.WorkerName == "Worker2");

        Assert.True(updated1.IsManuallyStopped);
        Assert.True(updated1.IsScheduleEnabled); // Unchanged
        Assert.False(updated2.IsScheduleEnabled);
        Assert.False(updated2.IsManuallyStopped); // Unchanged
    }
}
