using System;
using System.Collections.Generic;

namespace Mrg32k3a.NET.Benchmarks;

/// <summary>
/// Replays each timed loop once, untimed, and records the value its outputs fold into.
/// </summary>
/// <remarks>
/// <para>
/// The checksum serves two purposes. Within one machine it makes a run self-checking: the same case
/// at the same iteration count must always produce the same value, which is the cheapest available
/// proof that the optimizer did not quietly delete the loop being timed. Across implementations it
/// is the thing to agree on before any timing is compared, because two generators are only
/// comparable if they did the same work.
/// </para>
/// <para>
/// Every case starts from the first stream of a freshly seeded factory, so the value depends on
/// nothing but the case name and the iteration count.
/// </para>
/// </remarks>
internal static class Checksums
{
    /// <summary>Computes the sink of every case that produces output.</summary>
    /// <returns>A map from case name to sink value; cases that produce no output are absent.</returns>
    internal static IReadOnlyDictionary<string, double> Compute()
    {
        return new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["u01_plain"] = NextDoubles(antithetic: false, highPrecision: false),
            ["u01_antithetic"] = NextDoubles(antithetic: true, highPrecision: false),
            ["u01_incprec"] = NextDoubles(antithetic: false, highPrecision: true),
            ["u01_both"] = NextDoubles(antithetic: true, highPrecision: true),
            ["randint_1_10"] = Int32Draws(antithetic: false, highPrecision: false),
            ["randint_1_10_int64"] = Int64Draws(),
            ["randint_both"] = Int32Draws(antithetic: true, highPrecision: true),
            ["multistream_64"] = Multistream(),
            ["reset_next_substream"] = SkipToNextSubstream(),
            ["rngstream_as_random"] = Adapter(),
        };
    }

    private static double NextDoubles(bool antithetic, bool highPrecision)
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.Antithetic = antithetic;
        stream.HighPrecision = highPrecision;
        var sum = 0.0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += stream.NextDouble();
        }

        return sum;
    }

    private static double Int32Draws(bool antithetic, bool highPrecision)
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.Antithetic = antithetic;
        stream.HighPrecision = highPrecision;
        long sum = 0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += stream.NextInt32Inclusive(1, 10);
        }

        return sum;
    }

    private static double Int64Draws()
    {
        var stream = new RandomStreamFactory().CreateStream();
        long sum = 0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += stream.NextInt64Inclusive(1, 10);
        }

        return sum;
    }

    private static double Multistream()
    {
        var factory = new RandomStreamFactory();
        var streams = new RandomStream[Cases.MultistreamCount];
        for (var i = 0; i < streams.Length; i++)
        {
            streams[i] = factory.CreateStream();
        }

        var sum = 0.0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += streams[i % Cases.MultistreamCount].NextDouble();
        }

        return sum;
    }

    private static double SkipToNextSubstream()
    {
        var stream = new RandomStreamFactory().CreateStream();
        for (var i = 0; i < 1000; i++)
        {
            stream.NextDouble();
        }

        var sum = 0.0;
        for (var i = 0; i < Cases.Lifecycle; i++)
        {
            stream.SkipToNextSubstream();
            sum += stream.NextDouble();
        }

        return sum;
    }

    private static double Adapter()
    {
        Random adapter = new StreamBackedRandom(new RandomStreamFactory().CreateStream());
        var sum = 0.0;
        for (var i = 0; i < Cases.Draws; i++)
        {
            sum += adapter.NextDouble();
        }

        return sum;
    }
}
