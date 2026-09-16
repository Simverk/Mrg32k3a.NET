using System;
using BenchmarkDotNet.Attributes;

namespace Mrg32k3a.NET.Benchmarks;

/// <summary>
/// The framework's own generators, measured under the same loop shape so the cost of the
/// reproducibility guarantee can be read in familiar units.
/// </summary>
/// <remarks>
/// <see cref="Random"/> is free to change its algorithm between releases and offers no streams, no
/// substreams and no jumps, so these rows are only for context. The adapter row
/// shows what dispatching through <see cref="Random"/> adds on top of a stream.
/// </remarks>
[BenchmarkCategory(Cases.Baselines)]
public class BaselineBenchmarks
{
    private Random _random = null!;
    private Random _adapter = null!;

    /// <summary>Builds the framework generator and the adapter over a stream.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _random = new Random(12345);
        _adapter = new StreamBackedRandom(new RandomStreamFactory().CreateStream());
    }

    /// <summary>Draws from a seeded framework generator.</summary>
    /// <returns>The sink the draws fold into.</returns>
    [Benchmark(Description = "system_random", OperationsPerInvoke = Cases.Draws)]
    public double SystemRandom()
    {
        var sum = 0.0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += _random.NextDouble();
        }

        return sum;
    }

    /// <summary>Draws from the thread-shared framework generator.</summary>
    /// <returns>The sink the draws fold into.</returns>
    [Benchmark(Description = "system_random_shared", OperationsPerInvoke = Cases.Draws)]
    public double SystemRandomShared()
    {
        var sum = 0.0;
        var shared = Random.Shared;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += shared.NextDouble();
        }

        return sum;
    }

    /// <summary>Draws from a stream through the <see cref="Random"/> adapter.</summary>
    /// <returns>The sink the draws fold into.</returns>
    [Benchmark(Description = "rngstream_as_random", OperationsPerInvoke = Cases.Draws)]
    public double AdapterNextDouble()
    {
        var sum = 0.0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += _adapter.NextDouble();
        }

        return sum;
    }
}
