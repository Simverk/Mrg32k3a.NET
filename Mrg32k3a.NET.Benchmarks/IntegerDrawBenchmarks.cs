using BenchmarkDotNet.Attributes;

namespace Mrg32k3a.NET.Benchmarks;

/// <summary>
/// Throughput of the integer draws, which scale one uniform variate over a closed range.
/// </summary>
/// <remarks>
/// Both the 32 bit and the 64 bit form of the plain case are measured. They do the same work on the
/// backbone and differ only in their bounds arithmetic, but the 64 bit form is the one whose
/// signature matches implementations whose integer draw returns a wide integer, so having both
/// rows lets either comparison be made without re-running anything.
/// </remarks>
[BenchmarkCategory(Cases.Throughput)]
public class IntegerDrawBenchmarks
{
    private RandomStream _plain = null!;
    private RandomStream _plainWide = null!;
    private RandomStream _both = null!;

    /// <summary>Builds the streams the integer cases draw from.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _plain = new RandomStreamFactory().CreateStream();
        _plainWide = new RandomStreamFactory().CreateStream();
        _both = new RandomStreamFactory().CreateStream();
        _both.Antithetic = true;
        _both.HighPrecision = true;
    }

    /// <summary>Integers from one to ten inclusive, through the 32 bit overload.</summary>
    /// <returns>The sink the draws fold into.</returns>
    [Benchmark(Description = "randint_1_10", OperationsPerInvoke = Cases.Draws)]
    public long Int32Inclusive()
    {
        long sum = 0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += _plain.NextInt32Inclusive(1, 10);
        }

        return sum;
    }

    /// <summary>Integers from one to ten inclusive, through the 64 bit overload.</summary>
    /// <returns>The sink the draws fold into.</returns>
    [Benchmark(Description = "randint_1_10_int64", OperationsPerInvoke = Cases.Draws)]
    public long Int64Inclusive()
    {
        long sum = 0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += _plainWide.NextInt64Inclusive(1, 10);
        }

        return sum;
    }

    /// <summary>Integers from one to ten inclusive with both output flags set.</summary>
    /// <returns>The sink the draws fold into.</returns>
    [Benchmark(Description = "randint_both", OperationsPerInvoke = Cases.Draws)]
    public long Int32InclusiveBoth()
    {
        long sum = 0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += _both.NextInt32Inclusive(1, 10);
        }

        return sum;
    }
}
