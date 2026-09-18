using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Reports;

namespace Mrg32k3a.NET.Benchmarks;

/// <summary>
/// Turns the raw reports into a grouped table on the console and a flat CSV on disk.
/// </summary>
/// <remarks>
/// The default summary is laid out for reading one run in isolation. What this harness is for is
/// setting its numbers beside another implementation's, so the output here is organised for that:
/// one row per case under its stable case name, times in nanoseconds per operation, and a rate
/// column, with no scaled units to reconcile before two tables can be compared.
/// </remarks>
internal static class ResultsTable
{
    private static readonly string[] CaseOrder =
    {
        "u01_plain",
        "u01_antithetic",
        "u01_incprec",
        "u01_both",
        "randint_1_10",
        "randint_1_10_int64",
        "randint_both",
        "multistream_64",
        "advance_0_1",
        "advance_5_3",
        "advance_127_0",
        "reset_next_substream",
        "skip_to_substream_1000",
        "create_stream",
        "system_random",
        "system_random_shared",
        "rngstream_as_random",
    };

    private static readonly string[] SectionOrder =
    {
        Cases.Throughput,
        Cases.Jumps,
        Cases.LifecycleSection,
        Cases.Baselines,
    };

    /// <summary>Prints the grouped table and writes the CSV.</summary>
    /// <param name="summaries">The summaries produced by the run.</param>
    /// <param name="checksums">The sink of each case that produces output.</param>
    /// <param name="outputPath">Where to write the CSV.</param>
    internal static void Render(
        IEnumerable<Summary> summaries,
        IReadOnlyDictionary<string, double> checksums,
        string outputPath)
    {
        var rows = Collect(summaries, checksums);
        if (rows.Count == 0)
        {
            Console.WriteLine("No benchmark results to report.");
            return;
        }

        var header = Header();
        WriteConsole(header, rows);
        WriteCsv(header, rows, outputPath);
    }

    private static List<Row> Collect(
        IEnumerable<Summary> summaries,
        IReadOnlyDictionary<string, double> checksums)
    {
        var byName = new Dictionary<string, Row>(StringComparer.Ordinal);
        foreach (var summary in summaries)
        {
            foreach (var report in summary.Reports)
            {
                var statistics = report.ResultStatistics;
                if (statistics is null)
                {
                    continue;
                }

                var descriptor = report.BenchmarkCase.Descriptor;
                var name = descriptor.WorkloadMethodDisplayInfo;
                var section = descriptor.Categories.Length != 0 ? descriptor.Categories[0] : "Other";
                checksums.TryGetValue(name, out var checksum);
                byName[name] = new Row(
                    name,
                    section,
                    descriptor.OperationsPerInvoke,
                    statistics.Median,
                    statistics.Min,
                    report.GcStats.GetBytesAllocatedPerOperation(report.BenchmarkCase),
                    checksums.ContainsKey(name) ? checksum : null);
            }
        }

        var ordered = new List<Row>();
        foreach (var name in CaseOrder)
        {
            if (byName.TryGetValue(name, out var row))
            {
                ordered.Add(row);
                byName.Remove(name);
            }
        }

        ordered.AddRange(byName.Values.OrderBy(r => r.Name, StringComparer.Ordinal));
        return ordered;
    }

    private static void WriteConsole(IReadOnlyList<string> header, IReadOnlyList<Row> rows)
    {
        var text = new StringBuilder();
        text.AppendLine();
        foreach (var line in header)
        {
            text.Append("# ").AppendLine(line);
        }

        var sections = SectionOrder
            .Concat(rows.Select(r => r.Section))
            .Distinct(StringComparer.Ordinal);

        foreach (var section in sections)
        {
            var members = rows.Where(r => string.Equals(r.Section, section, StringComparison.Ordinal)).ToList();
            if (members.Count == 0)
            {
                continue;
            }

            var perDraw = string.Equals(section, Cases.Throughput, StringComparison.Ordinal)
                || string.Equals(section, Cases.Baselines, StringComparison.Ordinal);
            var unit = perDraw ? "ns/draw" : "ns/op";
            var rate = perDraw ? "draws/s" : "ops/s";
            var last = perDraw ? "checksum" : "alloc B/op";

            text.AppendLine();
            text.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0,-24}{1,12}{2,12}{3,16}{4,22}",
                section,
                unit,
                "best",
                rate,
                last);
            text.AppendLine();
            text.AppendLine(new string('-', 86));

            foreach (var row in members)
            {
                text.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "  {0,-22}{1,12}{2,12}{3,16}{4,22}",
                    row.Name,
                    Time(row.MedianNanoseconds),
                    Time(row.MinNanoseconds),
                    row.OperationsPerSecond.ToString("N0", CultureInfo.InvariantCulture),
                    perDraw ? Checksum(row.Checksum) : Bytes(row.AllocatedBytes));
                text.AppendLine();
            }
        }

        text.AppendLine();
        foreach (var note in Notes)
        {
            text.Append("note: ").AppendLine(note);
        }

        Console.Write(text.ToString());
    }

    private static void WriteCsv(IReadOnlyList<string> header, IReadOnlyList<Row> rows, string outputPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var text = new StringBuilder();
        foreach (var line in header)
        {
            text.Append("# ").AppendLine(line);
        }

        text.AppendLine("case,section,ops,median_ns_per_op,min_ns_per_op,ops_per_sec,alloc_bytes,checksum");
        foreach (var row in rows)
        {
            text.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3:F3},{4:F3},{5:F0},{6},{7}",
                row.Name,
                row.Section,
                row.OperationsPerInvoke,
                row.MedianNanoseconds,
                row.MinNanoseconds,
                row.OperationsPerSecond,
                row.AllocatedBytes.HasValue ? row.AllocatedBytes.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                row.Checksum.HasValue ? row.Checksum.Value.ToString("F6", CultureInfo.InvariantCulture) : string.Empty);
            text.AppendLine();
        }

        File.WriteAllText(outputPath, text.ToString());
        Console.WriteLine();
        Console.WriteLine("Wrote " + Path.GetFullPath(outputPath));
    }

    private static string Time(double nanoseconds)
    {
        var digits = nanoseconds >= 1000 ? 1 : 2;
        return nanoseconds.ToString("N" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    private static string Bytes(long? value)
    {
        return value.HasValue ? value.Value.ToString("N0", CultureInfo.InvariantCulture) : "-";
    }

    private static string Checksum(double? value)
    {
        return value.HasValue ? value.Value.ToString("F6", CultureInfo.InvariantCulture) : "-";
    }

    private static IReadOnlyList<string> Header()
    {
        return new[]
        {
            "Mrg32k3a.NET benchmark",
            "runtime  " + RuntimeInformation.FrameworkDescription + " (" + RuntimeInformation.ProcessArchitecture + ")",
            "os       " + RuntimeInformation.OSDescription,
            "cpu      " + ProcessorName(),
            "affinity " + Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture) + " logical processor(s) visible to this process",
            "commit   " + GitCommit(),
            "run      " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture),
            "checksum is the sink of the first N operations of a freshly seeded stream, N = the ops column",
        };
    }

    private static string ProcessorName()
    {
        try
        {
            const string path = "/proc/cpuinfo";
            if (File.Exists(path))
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (line.StartsWith("model name", StringComparison.Ordinal))
                    {
                        return line[(line.IndexOf(':') + 1)..].Trim();
                    }
                }
            }
        }
        catch (IOException)
        {
            // Fall through to the generic answer below.
        }

        return "unknown";
    }

    private static string GitCommit()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "rev-parse --short HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            if (process is null)
            {
                return "unknown";
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            return output.Length == 0 ? "unknown" : output;
        }
        catch (SystemException)
        {
            return "unknown";
        }
    }

    private static readonly string[] Notes =
    {
        "advance_5_3 applies two jump matrices; an implementation taking (e, c) in one call composes them and applies once.",
        "multistream_64 indexes with a constant modulus, which the JIT turns into masking; a runtime modulus costs a division.",
        "create_stream allocates one object and defers collection, and takes an uncontended lock; there is no free to pair with it.",
        "randint_1_10 uses the int overload and randint_1_10_int64 the long one; both do the same work on the backbone.",
    };

    private sealed record Row(
        string Name,
        string Section,
        int OperationsPerInvoke,
        double MedianNanoseconds,
        double MinNanoseconds,
        long? AllocatedBytes,
        double? Checksum)
    {
        internal double OperationsPerSecond => MedianNanoseconds > 0 ? 1e9 / MedianNanoseconds : 0;
    }
}
