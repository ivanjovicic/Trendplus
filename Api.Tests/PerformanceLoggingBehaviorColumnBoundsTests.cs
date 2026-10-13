using Application.Artikli.Common.Interfaces;
using Application.Behaviors;
using Application.Config;
using Application.Performance;
using Domain.Model;
using Infrastructure.DbContexts;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class PerformanceLoggingBehaviorColumnBoundsTests
{
    [Theory]
    [InlineData(null, 3, null)]
    [InlineData("", 3, "")]
    [InlineData("abc", 3, "abc")]
    [InlineData("abcdef", 5, "ab...")]
    [InlineData("abcdef", 3, "abc")]
    [InlineData("abcdef", 2, "ab")]
    [InlineData("abcdef", 0, "")]
    public void Truncate_RespectsRequestedMaximum(string? value, int maxLength, string? expected)
    {
        Assert.Equal(expected, PerformanceLogColumnBounds.Truncate(value, maxLength));
    }

    [Theory]
    [InlineData(PerformanceLogColumnBounds.RequestTypeMaxLength)]
    [InlineData(PerformanceLogColumnBounds.RequestNameMaxLength)]
    [InlineData(PerformanceLogColumnBounds.ExceptionMessageMaxLength)]
    [InlineData(PerformanceLogColumnBounds.PayloadMaxLength)]
    [InlineData(PerformanceLogColumnBounds.SerializedJsonMaxLength)]
    public void Truncate_NeverExceedsColumnLimit(int maxLength)
    {
        var longValue = new string('x', maxLength + 100);
        var truncated = PerformanceLogColumnBounds.Truncate(longValue, maxLength);

        Assert.NotNull(truncated);
        Assert.Equal(maxLength, truncated.Length);
        Assert.EndsWith("...", truncated);
    }

    [Fact]
    public void Truncate_PreservesSurrogatePairBoundaryWithoutThrowing()
    {
        var emoji = new string('a', PerformanceLogColumnBounds.RequestTypeMaxLength - 4) + "😀";
        var truncated = PerformanceLogColumnBounds.Truncate(emoji, PerformanceLogColumnBounds.RequestTypeMaxLength);

        Assert.NotNull(truncated);
        Assert.True(truncated.Length <= PerformanceLogColumnBounds.RequestTypeMaxLength);
    }

    [Fact]
    public async Task FailedRequest_PersistsBoundedFieldsWithinSchemaLimits()
    {
        await using var db = CreateAnalyticsDb();
        var behavior = CreateBehavior<HeavyPayloadRequest, LongToStringResponse>(db);
        var longException = new string('e', PerformanceLogColumnBounds.ExceptionMessageMaxLength + 250);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new HeavyPayloadRequest { Data = new string('d', 12_000) },
                CancellationToken.None,
                () => throw new InvalidOperationException(longException)));

        var log = Assert.Single(await db.PerformanceLogs.ToListAsync());
        AssertFieldWithinLimit(log.RequestType, PerformanceLogColumnBounds.RequestTypeMaxLength);
        AssertFieldWithinLimit(log.RequestName, PerformanceLogColumnBounds.RequestNameMaxLength);
        AssertFieldWithinLimit(log.ExceptionMessage, PerformanceLogColumnBounds.ExceptionMessageMaxLength);
        AssertFieldWithinLimit(log.RequestData, PerformanceLogColumnBounds.PayloadMaxLength);
        Assert.False(log.IsSuccess);
        Assert.NotNull(log.ExceptionMessage);
        Assert.EndsWith("...", log.ExceptionMessage);
    }

    [Fact]
    public async Task SlowSuccessfulRequest_BoundsLongJsonResponsePayload()
    {
        await using var db = CreateAnalyticsDb();
        var behavior = CreateBehavior<SlowSuccessRequest, string>(db);

        await behavior.Handle(
            new SlowSuccessRequest(),
            CancellationToken.None,
            async () =>
            {
                await Task.Delay(1_100);
                return new string('r', 12_000);
            });

        var log = Assert.Single(await db.PerformanceLogs.ToListAsync());
        AssertFieldWithinLimit(log.RequestType, PerformanceLogColumnBounds.RequestTypeMaxLength);
        AssertFieldWithinLimit(log.ResponseData, PerformanceLogColumnBounds.PayloadMaxLength);
        Assert.Equal(PerformanceLogColumnBounds.SerializedJsonMaxLength, log.ResponseData!.Length);
        Assert.True(log.IsSuccess);
        Assert.EndsWith("...", log.ResponseData);
    }

    [Fact]
    public async Task FailedRequest_WithEmptyExceptionMessage_PersistsEmptyExceptionColumn()
    {
        await using var db = CreateAnalyticsDb();
        var behavior = CreateBehavior<MinimalFailRequest, string>(db);

        await Assert.ThrowsAsync<Exception>(() =>
            behavior.Handle(
                new MinimalFailRequest(),
                CancellationToken.None,
                () => throw new Exception(string.Empty)));

        var log = Assert.Single(await db.PerformanceLogs.ToListAsync());
        Assert.Equal(string.Empty, log.ExceptionMessage);
        Assert.False(log.IsSuccess);
    }

    [Fact]
    public async Task SerializationFailureFallback_IsBoundedToPayloadMaxLength()
    {
        await using var db = CreateAnalyticsDb();
        var behavior = CreateBehavior<UnserializablePayloadRequest, string>(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new UnserializablePayloadRequest(),
                CancellationToken.None,
                () => throw new InvalidOperationException("fail")));

        var log = Assert.Single(await db.PerformanceLogs.ToListAsync());
        AssertFieldWithinLimit(log.RequestData, PerformanceLogColumnBounds.PayloadMaxLength);
        Assert.Equal(PerformanceLogColumnBounds.PayloadMaxLength, log.RequestData!.Length);
        Assert.EndsWith("...", log.RequestData);
    }

    [Fact]
    public async Task LongMediatRTypeFullName_IsBoundedOnRequestType()
    {
        await using var db = CreateAnalyticsDb();
        var behavior = CreateBehavior<
            Rq638LongRequestTypes.ExtraLongNamespaceSegmentOne.ExtraLongNamespaceSegmentTwo.ExtraLongNamespaceSegmentThree.ExtraLongNamespaceSegmentFour.ExtraLongNamespaceSegmentFive.ExtraLongNamespaceSegmentSix.ExtraLongNamespaceSegmentSeven.ExtraLongNamespaceSegmentEight.
                Rq638AdditionalLongMediatRRequestTypeNameForRequestTypeColumnBoundVerificationABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789End,
            string>(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new Rq638LongRequestTypes.ExtraLongNamespaceSegmentOne.ExtraLongNamespaceSegmentTwo.ExtraLongNamespaceSegmentThree.ExtraLongNamespaceSegmentFour.ExtraLongNamespaceSegmentFive.ExtraLongNamespaceSegmentSix.ExtraLongNamespaceSegmentSeven.ExtraLongNamespaceSegmentEight.
                    Rq638AdditionalLongMediatRRequestTypeNameForRequestTypeColumnBoundVerificationABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789End(),
                CancellationToken.None,
                () => throw new InvalidOperationException("fail")));

        var log = Assert.Single(await db.PerformanceLogs.ToListAsync());
        Assert.Equal(PerformanceLogColumnBounds.RequestTypeMaxLength, log.RequestType.Length);
        Assert.EndsWith("...", log.RequestType);
    }

    private static void AssertFieldWithinLimit(string? value, int maxLength)
    {
        if (value is null)
        {
            return;
        }

        Assert.True(value.Length <= maxLength, $"Expected length <= {maxLength}, got {value.Length}");
    }

    private static AnalyticsDbContext CreateAnalyticsDb()
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AnalyticsDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static PerformanceLoggingBehavior<TRequest, TResponse> CreateBehavior<TRequest, TResponse>(
        IAnalyticsDbContext analyticsDb)
        where TRequest : IRequest<TResponse>
    {
        return new PerformanceLoggingBehavior<TRequest, TResponse>(
            NullLogger<PerformanceLoggingBehavior<TRequest, TResponse>>.Instance,
            analyticsDb,
            new HttpContextAccessor(),
            Options.Create(new PerformanceLoggingOptions()));
    }

    private sealed class HeavyPayloadRequest : IRequest<LongToStringResponse>
    {
        public string Data { get; set; } = string.Empty;
    }

    private sealed class SlowSuccessRequest : IRequest<string>;

    private sealed class MinimalFailRequest : IRequest<string>;

    private sealed class UnserializablePayloadRequest : IRequest<string>
    {
        public Action Callback => () => { };

        public override string ToString() => new string('u', 8_000);
    }

    private sealed class LongToStringResponse
    {
        public override string ToString() => new string('r', 8_000);
    }

}
