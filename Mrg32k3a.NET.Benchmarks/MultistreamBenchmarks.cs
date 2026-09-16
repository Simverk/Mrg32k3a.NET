using BenchmarkDotNet.Attributes;

namespace Mrg32k3a.NET.Benchmarks;

/// <summary>
/// Draws taken in rotation across many streams, so that no stream's state stays in the innermost
/// cache between its own draws.
/// </summary>
/// <remarks>
/// This is the case that separates the cost of the arithmetic from the cost of reaching the state.
/// A single-stream loop keeps six values in registers across iterations; a simulation that runs one
/// stream per entity does not, and this measures what that costs.
/// </remarks>
[BenchmarkCategory(Cases.Throughput)]
public class MultistreamBenchmarks
{
    private RandomStream[] _streams = null!;

    /// <summary>Builds the rotation of streams from a freshly seeded factory.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var factory = new RandomStreamFactory();
        _streams = new RandomStream[Cases.MultistreamCount];
        for (var i = 0; i < _streams.Length; i++)
        {
            _streams[i] = factory.CreateStream();
        }
    }

    /// <summary>One draw from each stream in turn, cycling.</summary>
    /// <returns>The sink the draws fold into.</returns>
    [Benchmark(Description = "multistream_64", OperationsPerInvoke = Cases.Draws)]
    public double Multistream()
    {
        var sum = 0.0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += _streams[i % Cases.MultistreamCount].NextDouble();
        }

        return sum;
    }
}
