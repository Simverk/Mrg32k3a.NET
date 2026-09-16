using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Mrg32k3a.NET.Benchmarks;

/// <summary>Entry point: runs the benchmarks, then reports them in comparison-ready form.</summary>
internal static class Program
{
    private const string DefaultOutput = "BenchmarkDotNet.Artifacts/results.csv";

    /// <summary>Runs the suite.</summary>
    /// <param name="args">
    /// <c>--out FILE</c> chooses where the CSV goes; everything else is passed straight through to
    /// BenchmarkDotNet, so <c>--filter</c>, <c>--list</c> and the rest work as usual.
    /// </param>
    /// <returns>Zero on success, one if no benchmark ran.</returns>
    internal static int Main(string[] args)
    {
        var outputPath = DefaultOutput;
        var remaining = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--out", StringComparison.Ordinal) && i + 1 < args.Length)
            {
                outputPath = args[++i];
            }
            else
            {
                remaining.Add(args[i]);
            }
        }

        Console.WriteLine("Computing checksums...");
        var checksums = Checksums.Compute();

        var config = ManualConfig.Create(DefaultConfig.Instance)
            .WithOptions(ConfigOptions.DisableLogFile);

        var switcher = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly);
        var summaries = remaining.Count > 0
            ? switcher.Run(remaining.ToArray(), config)
            : switcher.RunAll(config);

        var completed = summaries as IReadOnlyList<Summary> ?? summaries.ToList();
        if (completed.Count == 0 || completed.All(s => s.Reports.Length == 0))
        {
            Console.WriteLine("No benchmarks ran.");
            return 1;
        }

        ResultsTable.Render(completed, checksums, outputPath);
        return 0;
    }
}
