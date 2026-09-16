using BenchmarkDotNet.Attributes;

namespace Mrg32k3a.NET.Benchmarks;

/// <summary>
/// Cost and footprint of obtaining a stream.
/// </summary>
/// <remarks>
/// Memory is diagnosed here so the allocated bytes per stream can be read off directly and compared
/// against the size of another implementation's stream object. A stream handed out by a factory is
/// one managed allocation plus a lock acquisition; there is no counterpart to freeing it, so the
/// deferred collection cost does not show up in the time column and the byte column is the honest
/// half of this comparison.
/// </remarks>
[MemoryDiagnoser]
[BenchmarkCategory(Cases.LifecycleSection)]
public class LifecycleBenchmarks
{
    private RandomStreamFactory _factory = null!;

    /// <summary>Builds the factory the created streams come from.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _factory = new RandomStreamFactory();
    }

    /// <summary>Taking a stream from an explicit factory.</summary>
    /// <returns>A value derived from each stream, so creation cannot be optimized away.</returns>
    [Benchmark(Description = "create_stream", OperationsPerInvoke = Cases.Lifecycle)]
    public long CreateStream()
    {
        long total = 0;
        for (var i = 0; i < Cases.Lifecycle; i++)
        {
            total += _factory.CreateStream().Name.Length;
        }

        return total;
    }
}
