using BenchmarkDotNet.Attributes;

namespace Mrg32k3a.NET.Benchmarks;

/// <summary>
/// Throughput of <see cref="RandomStream.NextDouble"/> under each combination of the two output flags.
/// </summary>
/// <remarks>
/// Each method is one tight loop over a stream built in <see cref="Setup"/>, so what is timed is the
/// same shape a caller drawing variates in bulk would get, and the per-invocation cost of the
/// harness is amortised over <see cref="Cases.Draws"/> draws. The recurrence is branch free and its
/// cost does not depend on the state, so letting the streams run on across iterations is free.
/// </remarks>
[BenchmarkCategory(Cases.Throughput)]
public class NextDoubleBenchmarks
{
    private RandomStream _plain = null!;
    private RandomStream _antithetic = null!;
    private RandomStream _highPrecision = null!;
    private RandomStream _both = null!;

    /// <summary>Builds one stream per flag combination, each the first stream of its own factory.</summary>
    /// <remarks>
    /// A factory of its own per case means every loop starts at the same canonical state, so the
    /// first pass of each loop draws exactly the sequence the reported checksum describes.
    /// </remarks>
    [GlobalSetup]
    public void Setup()
    {
        _plain = new RandomStreamFactory().CreateStream();
        _antithetic = new RandomStreamFactory().CreateStream();
        _antithetic.Antithetic = true;
        _highPrecision = new RandomStreamFactory().CreateStream();
        _highPrecision.HighPrecision = true;
        _both = new RandomStreamFactory().CreateStream();
        _both.Antithetic = true;
        _both.HighPrecision = true;
    }

    /// <summary>Plain draws, both flags off.</summary>
    /// <returns>The sink the draws fold into, returned so the loop cannot be optimized away.</returns>
    [Benchmark(Description = "u01_plain", OperationsPerInvoke = Cases.Draws)]
    public double NextDoublePlain()
    {
        var sum = 0.0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += _plain.NextDouble();
        }

        return sum;
    }

    /// <summary>Draws reflected about one half.</summary>
    /// <returns>The sink the draws fold into.</returns>
    [Benchmark(Description = "u01_antithetic", OperationsPerInvoke = Cases.Draws)]
    public double NextDoubleAntithetic()
    {
        var sum = 0.0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += _antithetic.NextDouble();
        }

        return sum;
    }

    /// <summary>Draws of roughly 53 bits, which consume two underlying draws each.</summary>
    /// <returns>The sink the draws fold into.</returns>
    [Benchmark(Description = "u01_incprec", OperationsPerInvoke = Cases.Draws)]
    public double NextDoubleHighPrecision()
    {
        var sum = 0.0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += _highPrecision.NextDouble();
        }

        return sum;
    }

    /// <summary>Draws with both flags set.</summary>
    /// <returns>The sink the draws fold into.</returns>
    [Benchmark(Description = "u01_both", OperationsPerInvoke = Cases.Draws)]
    public double NextDoubleBoth()
    {
        var sum = 0.0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += _both.NextDouble();
        }

        return sum;
    }
}
