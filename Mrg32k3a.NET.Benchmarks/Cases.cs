namespace Mrg32k3a.NET.Benchmarks;

/// <summary>
/// The vocabulary shared by the benchmark classes, the checksum pass and the reporter: case names,
/// the section each belongs to, and the number of operations each timed invocation performs.
/// </summary>
/// <remarks>
/// Case names are the join key when these results are lined up against another implementation's, so
/// they are written out as literals here and used verbatim in <c>[Benchmark(Description = ...)]</c>.
/// The operation counts are the inner-loop lengths; they must stay in step with the
/// <c>OperationsPerInvoke</c> arguments, which the language requires to be compile-time constants.
/// </remarks>
internal static class Cases
{
    /// <summary>Inner-loop length for every case that draws variates.</summary>
    internal const int Draws = 100_000;

    /// <summary>Inner-loop length for the two cheap jump operations.</summary>
    internal const int CheapJumps = 100;

    /// <summary>Inner-loop length for the full 2^127 matrix exponentiation.</summary>
    internal const int ExpensiveJumps = 10;

    /// <summary>Inner-loop length for substream resets and for stream creation.</summary>
    internal const int Lifecycle = 1_000;

    /// <summary>Number of streams the interleaved case strides through.</summary>
    internal const int MultistreamCount = 64;

    /// <summary>Section headings, in the order the report prints them.</summary>
    internal const string Throughput = "Throughput";

    /// <summary>Section heading for the jump operations.</summary>
    internal const string Jumps = "Jump operations";

    /// <summary>Section heading for stream creation and substream resets.</summary>
    internal const string LifecycleSection = "Lifecycle";

    /// <summary>Section heading for the framework generators measured for context.</summary>
    internal const string Baselines = ".NET baselines";
}
