using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints
{
    public static class AccessImportRestoreEndpoints
    {
        public static void MapAccessImportRestoreEndpoints(this WebApplication app)
        {
            app.MapPost("/api/access-import/cleanup/archive/restore-script", async (HttpRequest req, Api.Infrastructure.DbContexts.TrendplusDbContext _trendDb) =>
            {
                var body = await JsonSerializer.DeserializeAsync<RestoreRequest>(req.Body);
                var ids = body?.Ids ?? Array.Empty<int>();
                if (!ids.Any()) return Results.BadRequest(new { error = "ids required" });

                var idList = string.Join(',', ids.Select(i => i.ToString()));

                var sql = $@"
SELECT string_agg(sql_stmt, E'\n\n') FROM (
  SELECT 'INSERT INTO ""' || table_name || '"" SELECT * FROM jsonb_populate_record(NULL::""' || table_name || '"", ' || quote_literal(row_json::text) || ') ON CONFLICT DO NOTHING;' as sql_stmt
  FROM deleted_rows_archive WHERE id IN ({idList})
) s;";

                string script = string.Empty;
                var conn = _trendDb.Database.GetDbConnection();
                await conn.OpenAsync();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    var result = await cmd.ExecuteScalarAsync();
                    script = result?.ToString() ?? string.Empty;
                }
                finally
                {
                    await conn.CloseAsync();
                }

                return Results.Ok(new { script });
            }).WithTags("AccessImport");
        }

        private class RestoreRequest { public int[] Ids { get; set; } }
    }
}
