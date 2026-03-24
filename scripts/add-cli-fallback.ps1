$filePath = "c:\Users\Ivan\source\repos\Trendplus2\Api\Services\AccessImportService.cs"
$bytes = [System.IO.File]::ReadAllBytes($filePath)
$text = [System.Text.Encoding]::UTF8.GetString($bytes)

$marker = "private static void EnsurePlatformSupport()"
$idx = $text.IndexOf($marker)
if ($idx -lt 0) {
    Write-Host "ERROR: EnsurePlatformSupport not found!"
    exit 1
}

# Go to start of line
$lineStart = $text.LastIndexOf("`n", $idx) + 1
$before = $text.Substring(0, $lineStart)

$newCode = @'
    private static void EnsurePlatformSupport()
    {
        // No platform restriction - ODBC works on Windows, Linux, macOS, and Docker.
        // Falls back to mdb-tables / mdb-export CLI if ODBC driver is broken on Linux.
    }

    // ======================================================================
    // MDBTools CLI fallback - used when the ODBC driver fails on Linux/Docker
    // ======================================================================

    private static bool IsMdbToolsCliAvailable()
    {
        if (OperatingSystem.IsWindows()) return false;
        try
        {
            using var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "mdb-tables",
                Arguments = "--help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            proc.Start();
            proc.WaitForExit(3000);
            return true;
        }
        catch { return false; }
    }

    private static string RunMdbCli(string command, string args, int timeoutMs = 30000)
    {
        using var proc = new System.Diagnostics.Process();
        proc.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        proc.Start();
        var output = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(timeoutMs);
        if (proc.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
            throw new InvalidOperationException($"{command} failed: {stderr}");
        return output;
    }

    private static List<string> MdbCliGetTables(string filePath)
    {
        var output = RunMdbCli("mdb-tables", $"-1 \"{filePath}\"");
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !t.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static int MdbCliRowCount(string filePath, string tableName)
    {
        try
        {
            var csv = RunMdbCli("mdb-export", $"-H \"{filePath}\" \"{tableName}\"");
            return csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        }
        catch { return 0; }
    }

    private static HashSet<string> MdbCliGetColumns(string filePath, string tableName)
    {
        var cols = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var csv = RunMdbCli("mdb-export", $"\"{filePath}\" \"{tableName}\"");
            var header = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (header is null) return cols;
            foreach (var col in ParseCsvLine(header))
            {
                var clean = col.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(clean))
                    cols.Add(Normalize(clean));
            }
        }
        catch { }
        return cols;
    }

    private static IEnumerable<Dictionary<string, object?>> MdbCliReadRows(string filePath, string tableName)
    {
        string csv;
        try
        {
            csv = RunMdbCli("mdb-export", $"\"{filePath}\" \"{tableName}\"", timeoutMs: 60000);
        }
        catch { yield break; }

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) yield break;

        var headers = ParseCsvLine(lines[0]);
        var normalizedHeaders = headers.Select(h => Normalize(h.Trim().Trim('"'))).ToArray();

        for (var i = 1; i < lines.Length; i++)
        {
            var values = ParseCsvLine(lines[i]);
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < normalizedHeaders.Length && j < values.Length; j++)
            {
                var val = values[j].Trim().Trim('"');
                row[normalizedHeaders[j]] = string.IsNullOrEmpty(val) ? null : (object)val;
            }
            yield return row;
        }
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var inQuote = false;
        var current = new StringBuilder();

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuote = !inQuote;
                current.Append(ch);
            }
            else if (ch == ',' && !inQuote)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
'@

$newText = $before + $newCode
$newBytes = [System.Text.Encoding]::UTF8.GetBytes($newText)
[System.IO.File]::WriteAllBytes($filePath, $newBytes)

Write-Host "SUCCESS: Replaced EnsurePlatformSupport and added CLI fallback methods"
Write-Host "Old size: $($bytes.Length) bytes"
Write-Host "New size: $($newBytes.Length) bytes"
