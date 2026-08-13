using System.Collections;
using System.Data;
using System.Data.Common;
using Application.Analytics.Queries.GetInventoryAlerts;
using Application.Analytics.Queries.GetInventoryForecast;
using Application.Analytics.Queries.GetInventorySizeCurve;
using Application.Analytics.Queries.GetRebalanceSuggestions;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Domain.Model.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public sealed class InventorySnapshotContractTests
{
    [Fact(DisplayName = "Forecast snapshot preserves true zero and missing evidence")]
    public async Task ForecastHandler_PreservesZeroAndNullEvidence()
    {
        var table = CreateTable(
            ("sku_id", typeof(int)),
            ("store_id", typeof(int)),
            ("size_code", typeof(string)),
            ("forecast_7d", typeof(decimal)),
            ("forecast_14d", typeof(decimal)),
            ("forecast_28d", typeof(decimal)),
            ("probability_of_oos_in_7d", typeof(decimal)),
            ("overstock_risk", typeof(decimal)),
            ("confidence_score", typeof(decimal)),
            ("explanation", typeof(string)),
            ("total_matching_count", typeof(long)));
        table.Rows.Add(101, 7, "42", 0m, DBNull.Value, 4.5m, 0m, DBNull.Value, 0.95m, "Signal", 2L);

        var context = CreateContext(table);
        var handler = new GetInventoryForecastHandler(context, NullLogger<GetInventoryForecastHandler>.Instance);

        var result = await handler.Handle(new GetInventoryForecastQuery(Top: 1), CancellationToken.None);

        Assert.True(result.SnapshotAvailable);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.ReturnedCount);
        Assert.Equal(2, result.TotalMatchingCount);
        Assert.True(result.IsTruncated);
        Assert.Null(result.Items[0].Forecast14d);
        Assert.Null(result.Items[0].OverstockRisk);
        Assert.Equal(0m, result.Items[0].Forecast7d);
        Assert.Equal(0m, result.Items[0].ProbabilityOfOOSIn7d);
        Assert.Equal(0.95m, result.Items[0].ConfidenceScore);
        Assert.Equal("Forecast snapshot sadrzi redove sa nepotpunom signalnom evidencijom.", result.Warning);

        var commandText = context.Connection.LastCommandText ?? string.Empty;
        Assert.DoesNotContain("coalesce(forecast_7d, 0)", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(probability_of_oos_in_7d, 0)", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(overstock_risk, 0)", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(confidence_score, 0)", commandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Rebalance snapshot preserves true zero and missing evidence")]
    public async Task RebalanceHandler_PreservesZeroAndNullEvidence()
    {
        var table = CreateTable(
            ("from_store_id", typeof(int)),
            ("to_store_id", typeof(int)),
            ("sku_id", typeof(int)),
            ("size_code", typeof(string)),
            ("recommended_qty", typeof(int)),
            ("urgency", typeof(string)),
            ("confidence", typeof(decimal)),
            ("reason", typeof(string)),
            ("expected_saved_sales", typeof(decimal)),
            ("expected_capital_release", typeof(decimal)),
            ("total_matching_count", typeof(long)));
        table.Rows.Add(1, 2, 101, "42", 0, DBNull.Value, 0m, "Signal", 0m, DBNull.Value, 2L);

        var context = CreateContext(table);
        var handler = new GetRebalanceSuggestionsHandler(context, NullLogger<GetRebalanceSuggestionsHandler>.Instance);

        var result = await handler.Handle(new GetRebalanceSuggestionsQuery(Top: 1), CancellationToken.None);

        Assert.True(result.SnapshotAvailable);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.ReturnedCount);
        Assert.Equal(2, result.TotalMatchingCount);
        Assert.True(result.IsTruncated);
        Assert.Equal(0, result.Items[0].RecommendedQty);
        Assert.Null(result.Items[0].Urgency);
        Assert.Equal(0m, result.Items[0].Confidence);
        Assert.Equal(0m, result.Items[0].ExpectedSavedSales);
        Assert.Null(result.Items[0].ExpectedCapitalRelease);
        Assert.Equal("Rebalance snapshot sadrzi redove sa nepotpunom signalnom evidencijom.", result.Warning);

        var commandText = context.Connection.LastCommandText ?? string.Empty;
        Assert.DoesNotContain("coalesce(recommended_qty, 0)", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(urgency, 'normal')", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(confidence, 0)", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(expected_saved_sales, 0)", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(expected_capital_release, 0)", commandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Alert snapshot preserves true zero and missing evidence")]
    public async Task AlertsHandler_PreservesZeroAndNullEvidence()
    {
        var table = CreateTable(
            ("alert_type", typeof(string)),
            ("sku_id", typeof(int)),
            ("store_id", typeof(int)),
            ("size_code", typeof(string)),
            ("severity", typeof(string)),
            ("title", typeof(string)),
            ("message", typeof(string)),
            ("confidence_score", typeof(decimal)),
            ("total_matching_count", typeof(long)));
        table.Rows.Add("inventory_missing", 101, 7, "42", DBNull.Value, "Alert", "Message", 0m, 2L);

        var context = CreateContext(table);
        var handler = new GetInventoryAlertsHandler(context, NullLogger<GetInventoryAlertsHandler>.Instance);

        var result = await handler.Handle(new GetInventoryAlertsQuery(Top: 1), CancellationToken.None);

        Assert.True(result.SnapshotAvailable);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.ReturnedCount);
        Assert.Equal(2, result.TotalMatchingCount);
        Assert.True(result.IsTruncated);
        Assert.Null(result.Items[0].Severity);
        Assert.Equal(0m, result.Items[0].ConfidenceScore);
        Assert.Equal("Inventory alert snapshot sadrzi redove sa nepotpunom signalnom evidencijom.", result.Warning);

        var commandText = context.Connection.LastCommandText ?? string.Empty;
        Assert.DoesNotContain("coalesce(severity, 'info')", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(confidence_score, 0)", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("else 3", commandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Size curve snapshot preserves true zero and missing evidence")]
    public async Task SizeCurveHandler_PreservesZeroAndNullEvidence()
    {
        var table = CreateTable(
            ("sku_id", typeof(int)),
            ("store_id", typeof(int)),
            ("size_code", typeof(string)),
            ("actual_size_share", typeof(decimal)),
            ("ideal_size_share", typeof(decimal)),
            ("deviation_pct", typeof(decimal)),
            ("is_core_size_missing", typeof(bool)),
            ("is_dead_size", typeof(bool)),
            ("broken_run", typeof(bool)),
            ("curve_confidence", typeof(decimal)),
            ("reason_codes", typeof(string)),
            ("total_matching_count", typeof(long)));
        table.Rows.Add(101, 7, "42", 0m, DBNull.Value, 0m, false, DBNull.Value, false, 0m, "signal_a,signal_b", 2L);

        var context = CreateContext(table);
        var handler = new GetInventorySizeCurveHandler(context, NullLogger<GetInventorySizeCurveHandler>.Instance);

        var result = await handler.Handle(new GetInventorySizeCurveQuery(Top: 1), CancellationToken.None);

        Assert.True(result.SnapshotAvailable);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.ReturnedCount);
        Assert.Equal(2, result.TotalMatchingCount);
        Assert.True(result.IsTruncated);
        Assert.Equal(0m, result.Items[0].ActualSizeShare);
        Assert.Null(result.Items[0].IdealSizeShare);
        Assert.Equal(0m, result.Items[0].DeviationPct);
        Assert.False(result.Items[0].IsCoreSizeMissing ?? true);
        Assert.Null(result.Items[0].IsDeadSize);
        Assert.False(result.Items[0].BrokenRun ?? true);
        Assert.Equal(0m, result.Items[0].CurveConfidence);
        Assert.Equal("missing", result.Items[0].EvidenceStatus);
        Assert.Equal("Size curve snapshot sadrzi redove sa nepotpunom signalnom evidencijom.", result.Warning);

        var commandText = context.Connection.LastCommandText ?? string.Empty;
        Assert.DoesNotContain("coalesce(actual_size_share, 0)", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(ideal_size_share, 0)", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(deviation_pct, 0)", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(is_core_size_missing, false)", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(is_dead_size, false)", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(broken_run, false)", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coalesce(curve_confidence, 0)", commandText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Forecast snapshot keeps matching count at zero on empty reader without post-EOF access")]
    public async Task ForecastHandler_EmptySnapshotDoesNotReadCountAfterEof()
    {
        var table = CreateTable(
            ("sku_id", typeof(int)),
            ("store_id", typeof(int)),
            ("size_code", typeof(string)),
            ("forecast_7d", typeof(decimal)),
            ("forecast_14d", typeof(decimal)),
            ("forecast_28d", typeof(decimal)),
            ("probability_of_oos_in_7d", typeof(decimal)),
            ("overstock_risk", typeof(decimal)),
            ("confidence_score", typeof(decimal)),
            ("explanation", typeof(string)),
            ("total_matching_count", typeof(long)));

        var context = CreateContext(table);
        var handler = new GetInventoryForecastHandler(context, NullLogger<GetInventoryForecastHandler>.Instance);

        var result = await handler.Handle(new GetInventoryForecastQuery(Top: 1), CancellationToken.None);

        Assert.True(result.SnapshotAvailable);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.ReturnedCount);
        Assert.Equal(0, result.TotalMatchingCount);
        Assert.False(result.IsTruncated);
        Assert.Empty(result.Items);
        Assert.Equal("Forecast snapshot postoji, ali nema redova za trazene filtere.", result.Warning);
    }

    private static RecordingAnalyticsDbContext CreateContext(DataTable table) => new(new RecordingDbConnection(table));

    private static DataTable CreateTable(params (string Name, Type Type)[] columns)
    {
        var table = new DataTable();
        foreach (var (name, type) in columns)
        {
            table.Columns.Add(name, type);
        }

        return table;
    }

    private sealed class RecordingAnalyticsDbContext : IAnalyticsDbContext
    {
        public RecordingAnalyticsDbContext(RecordingDbConnection connection)
        {
            Connection = connection;
        }

        public RecordingDbConnection Connection { get; }

        public DbSet<ProductsDim> ProductsDim => null!;
        public DbSet<StoresDim> StoresDim => null!;
        public DbSet<PerformanceLog> PerformanceLogs => null!;
        public DbSet<SalesFact> SalesFacts => null!;
        public DbSet<SalesLineFact> SalesLineFacts => null!;
        public DbSet<SuppliersDim> SuppliersDim => null!;
        public DbSet<SeasonsDim> SeasonsDim => null!;
        public DbSet<FootwearTypesDim> FootwearTypesDim => null!;
        public DbSet<InventoryMovementFact> InventoryMovementFacts => null!;
        public DbSet<ReturnFact> ReturnFacts => null!;
        public DbSet<TrendProductSnapshot> TrendProductSnapshots => null!;
        public DbSet<TrendProductMomentum> TrendProductMomentums => null!;
        public DbSet<TrendplusIndexRecord> TrendplusIndexRecords => null!;
        public DbSet<InventoryRecommendation> InventoryRecommendations => null!;
        public DbSet<AnalyticsActionItem> AnalyticsActionItems => null!;
        public DbSet<AnalyticsActionNote> AnalyticsActionNotes => null!;

        public DbConnection GetDbConnection() => Connection;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class RecordingDbConnection : DbConnection
    {
        private readonly DataTable _table;
        private ConnectionState _state = ConnectionState.Closed;

        public RecordingDbConnection(DataTable table)
        {
            _table = table;
        }

        public string? LastCommandText { get; private set; }

        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "test";
        public override string DataSource => "test";
        public override string ServerVersion => "1";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName)
        {
        }

        public override void Close()
        {
            _state = ConnectionState.Closed;
        }

        public override void Open()
        {
            _state = ConnectionState.Open;
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            Open();
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new RecordingDbCommand(this, _table);

        internal void CaptureCommandText(string commandText) => LastCommandText = commandText;
    }

    private sealed class RecordingDbCommand : DbCommand
    {
        private readonly RecordingDbConnection _connection;
        private readonly DataTable _table;
        private readonly RecordingDbParameterCollection _parameters = new();
        private string _commandText = string.Empty;

        public RecordingDbCommand(RecordingDbConnection connection, DataTable table)
        {
            _connection = connection;
            _table = table;
        }

        public override string CommandText
        {
            get => _commandText;
            set
            {
                _commandText = value;
                _connection.CaptureCommandText(value);
            }
        }

        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection DbConnection
        {
            get => _connection;
            set => throw new NotSupportedException();
        }

        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => throw new NotSupportedException();

        public override object? ExecuteScalar() => throw new NotSupportedException();

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter() => new NpgsqlParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
            new EofStrictDbDataReader(_table.CreateDataReader());

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
            Task.FromResult<DbDataReader>(new EofStrictDbDataReader(_table.CreateDataReader()));
    }

    /// <summary>
    /// DataTable readers keep the last row after EOF; Npgsql does not. Guard GetInt64 so post-loop count reads fail.
    /// </summary>
    private sealed class EofStrictDbDataReader : DbDataReader
    {
        private readonly DbDataReader _inner;
        private bool _onRow;

        public EofStrictDbDataReader(DbDataReader inner)
        {
            _inner = inner;
        }

        public override int Depth => _inner.Depth;
        public override int FieldCount => _inner.FieldCount;
        public override bool HasRows => _inner.HasRows;
        public override bool IsClosed => _inner.IsClosed;
        public override int RecordsAffected => _inner.RecordsAffected;
        public override object this[int ordinal] => OnRowValue(() => _inner[ordinal]);
        public override object this[string name] => OnRowValue(() => _inner[name]);

        public override bool Read()
        {
            _onRow = _inner.Read();
            return _onRow;
        }

        public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            _onRow = await _inner.ReadAsync(cancellationToken);
            return _onRow;
        }

        public override long GetInt64(int ordinal)
        {
            EnsureOnRow();
            return _inner.GetInt64(ordinal);
        }

        public override bool GetBoolean(int ordinal) => OnRowValue(() => _inner.GetBoolean(ordinal));
        public override byte GetByte(int ordinal) => OnRowValue(() => _inner.GetByte(ordinal));
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
            OnRowValue(() => _inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length));
        public override char GetChar(int ordinal) => OnRowValue(() => _inner.GetChar(ordinal));
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
            OnRowValue(() => _inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length));
        public override string GetDataTypeName(int ordinal) => _inner.GetDataTypeName(ordinal);
        public override DateTime GetDateTime(int ordinal) => OnRowValue(() => _inner.GetDateTime(ordinal));
        public override decimal GetDecimal(int ordinal) => OnRowValue(() => _inner.GetDecimal(ordinal));
        public override double GetDouble(int ordinal) => OnRowValue(() => _inner.GetDouble(ordinal));
        public override IEnumerator GetEnumerator() => _inner.GetEnumerator();
        public override Type GetFieldType(int ordinal) => _inner.GetFieldType(ordinal);
        public override float GetFloat(int ordinal) => OnRowValue(() => _inner.GetFloat(ordinal));
        public override Guid GetGuid(int ordinal) => OnRowValue(() => _inner.GetGuid(ordinal));
        public override short GetInt16(int ordinal) => OnRowValue(() => _inner.GetInt16(ordinal));
        public override int GetInt32(int ordinal) => OnRowValue(() => _inner.GetInt32(ordinal));
        public override string GetName(int ordinal) => _inner.GetName(ordinal);
        public override int GetOrdinal(string name) => _inner.GetOrdinal(name);
        public override string GetString(int ordinal) => OnRowValue(() => _inner.GetString(ordinal));
        public override object GetValue(int ordinal) => OnRowValue(() => _inner.GetValue(ordinal));
        public override int GetValues(object[] values) => OnRowValue(() => _inner.GetValues(values));
        public override bool IsDBNull(int ordinal) => OnRowValue(() => _inner.IsDBNull(ordinal));
        public override bool NextResult() => _inner.NextResult();

        public override void Close()
        {
            _onRow = false;
            _inner.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void EnsureOnRow()
        {
            if (!_onRow)
            {
                throw new InvalidOperationException("No data exists for the row/column.");
            }
        }

        private T OnRowValue<T>(Func<T> read)
        {
            EnsureOnRow();
            return read();
        }
    }

    private sealed class RecordingDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _parameters = new();

        public override int Add(object value)
        {
            _parameters.Add((DbParameter)value);
            return _parameters.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
            {
                Add(value!);
            }
        }

        public override void Clear() => _parameters.Clear();

        public override bool Contains(object value) => _parameters.Contains((DbParameter)value);

        public override bool Contains(string value) => _parameters.Any(parameter => parameter.ParameterName == value);

        public override void CopyTo(Array array, int index) => ((ICollection)_parameters).CopyTo(array, index);

        public override int Count => _parameters.Count;

        public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();

        public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);

        public override int IndexOf(string parameterName) => _parameters.FindIndex(parameter => parameter.ParameterName == parameterName);

        public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);

        public override bool IsFixedSize => false;

        public override bool IsReadOnly => false;

        public override bool IsSynchronized => false;

        public override void Remove(object value) => _parameters.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _parameters.RemoveAt(index);

        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
            {
                _parameters.RemoveAt(index);
            }
        }

        protected override DbParameter GetParameter(int index) => _parameters[index];

        protected override DbParameter GetParameter(string parameterName) => _parameters[IndexOf(parameterName)];

        protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;

        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
            {
                _parameters[index] = value;
            }
            else
            {
                _parameters.Add(value);
            }
        }

        public override object SyncRoot => _parameters;
    }
}
