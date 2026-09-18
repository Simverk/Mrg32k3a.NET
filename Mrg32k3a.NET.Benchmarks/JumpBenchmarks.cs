using BenchmarkDotNet.Attributes;

namespace Mrg32k3a.NET.Benchmarks;

/// <summary>
/// Cost of moving a stream's position by an arbitrary number of steps, which is a pair of modular
/// matrix exponentiations rather than a walk of the recurrence.
/// </summary>
/// <remarks>
/// Memory is diagnosed here because it is part of the answer: every modular matrix multiply
/// behind these calls returns a fresh array, so the allocation count of a jump grows with
/// its exponent. The streams are nudged off their initial state in <see cref="Setup"/> so the
/// matrix-vector step operates on representative values rather than on the seed.
/// </remarks>
[MemoryDiagnoser]
[BenchmarkCategory(Cases.Jumps)]
public class JumpBenchmarks
{
    private RandomStream _byOne = null!;
    private RandomStream _byMixed = null!;
    private RandomStream _byStreamLength = null!;
    private RandomStream _substream = null!;
    private RandomStream _bySubstreamBlock = null!;

    /// <summary>Builds one stream per jump case and moves each off its seed.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var factory = new RandomStreamFactory();
        _byOne = Warmed(factory);
        _byMixed = Warmed(factory);
        _byStreamLength = Warmed(factory);
        _substream = Warmed(factory);
        _bySubstreamBlock = Warmed(factory);
    }

    /// <summary>A single forward step, taken through the general jump path.</summary>
    [Benchmark(Description = "advance_0_1", OperationsPerInvoke = Cases.CheapJumps)]
    public void AdvanceByOne()
    {
        for (var i = 0; i < Cases.CheapJumps; i++)
        {
            _byOne.Advance(1);
        }
    }

    /// <summary>A jump of 2^5 steps followed by a jump of 3 steps.</summary>
    /// <remarks>
    /// An implementation that takes the exponent and the offset in one call can compose the two
    /// matrices and apply the product once; taking them as two calls applies twice, so this case
    /// carries one extra matrix-vector step.
    /// </remarks>
    [Benchmark(Description = "advance_5_3", OperationsPerInvoke = Cases.CheapJumps)]
    public void AdvanceByMixed()
    {
        for (var i = 0; i < Cases.CheapJumps; i++)
        {
            _byMixed.AdvanceByPowerOfTwo(5);
            _byMixed.Advance(3);
        }
    }

    /// <summary>A full stream-length jump, 127 squarings of each component matrix.</summary>
    [Benchmark(Description = "advance_127_0", OperationsPerInvoke = Cases.ExpensiveJumps)]
    public void AdvanceByStreamLength()
    {
        for (var i = 0; i < Cases.ExpensiveJumps; i++)
        {
            _byStreamLength.AdvanceByPowerOfTwo(127);
        }
    }

    /// <summary>Moving to the next substream and then drawing from it.</summary>
    /// <remarks>
    /// A reset on its own draws nothing, so a draw is included: what a caller actually pays for is
    /// the reset-then-use pair, and the substream jump uses a precomputed 2^76 table rather than an
    /// exponentiation.
    /// </remarks>
    /// <returns>The sink the draws fold into.</returns>
    [Benchmark(Description = "reset_next_substream", OperationsPerInvoke = Cases.Lifecycle)]
    public double SkipToNextSubstream()
    {
        var sum = 0.0;
        for (var i = 0; i < Cases.Lifecycle; i++)
        {
            _substream.SkipToNextSubstream();
            sum += _substream.NextDouble();
        }

        return sum;
    }

    /// <summary>Landing on a substream a thousand places along, by index.</summary>
    /// <remarks>
    /// The comparison to draw is against <c>reset_next_substream</c>: that case pays one table jump
    /// per substream, so reaching the same place by walking would cost a thousand of them, while
    /// this is two modular matrix exponentiations whose cost grows with the logarithm of the index.
    /// The absolute form is timed rather than the relative one because it is anchored at the stream
    /// start, so the stream cannot drift across the many invocations the harness makes; the work
    /// the two do is the same. This case has no RngStreams counterpart, so unlike the others its
    /// name is not a join key against another implementation.
    /// </remarks>
    [Benchmark(Description = "skip_to_substream_1000", OperationsPerInvoke = Cases.ExpensiveJumps)]
    public void SkipToSubstreamByIndex()
    {
        for (var i = 0; i < Cases.ExpensiveJumps; i++)
        {
            _bySubstreamBlock.SkipToSubstream(Cases.SubstreamSkip);
        }
    }

    private static RandomStream Warmed(RandomStreamFactory factory)
    {
        var stream = factory.CreateStream();
        for (var i = 0; i < 1000; i++)
        {
            stream.NextDouble();
        }

        return stream;
    }
}
