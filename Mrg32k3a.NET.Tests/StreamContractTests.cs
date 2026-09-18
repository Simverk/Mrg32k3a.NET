namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Checks the stream and substream partitioning: where each stream starts, where each substream
/// starts, and what the reset operations mean.
/// </summary>
public class StreamContractTests
{
    private static readonly uint[] SecondStreamStart =
        { 3692455944, 1366884236, 2968912127, 335948734, 4161675175, 475798818 };

    private static readonly uint[] ThirdStreamStart =
        { 1015873554, 1310354410, 2249465273, 994084013, 2912484720, 3876682925 };

    private static readonly uint[] SecondSubstreamOfFirstStream =
        { 870504860, 2641697727, 884013853, 339352413, 2374306706, 3651603887 };

    [Fact]
    public void FirstStreamStartsAtTheFactorySeed()
    {
        var factory = new RandomStreamFactory();

        var stream = factory.CreateStream();

        Assert.Equal(factory.Seed, stream.StreamStartState);
        Assert.Equal(new uint[] { 12345, 12345, 12345, 12345, 12345, 12345 }, stream.StreamStartState.ToArray());
    }

    [Fact]
    public void SuccessiveStreamsStartOneStreamLengthApart()
    {
        var factory = new RandomStreamFactory();

        factory.CreateStream();
        var second = factory.CreateStream();
        var third = factory.CreateStream();

        Assert.Equal(SecondStreamStart, second.StreamStartState.ToArray());
        Assert.Equal(ThirdStreamStart, third.StreamStartState.ToArray());
    }

    [Fact]
    public void SecondStreamProducesItsDerivedValues()
    {
        var factory = new RandomStreamFactory();
        factory.CreateStream();
        var second = factory.CreateStream();

        Assert.Equal(0.7595818622487196, second.NextDouble());
        Assert.Equal(0.9783105732613708, second.NextDouble());
        Assert.Equal(0.6851358081931826, second.NextDouble());
    }

    [Fact]
    public void ThirdStreamProducesItsDerivedValues()
    {
        var factory = new RandomStreamFactory();
        factory.CreateStream();
        factory.CreateStream();
        var third = factory.CreateStream();

        Assert.Equal(0.7285097861965271, third.NextDouble());
        Assert.Equal(0.9655872822837334, third.NextDouble());
        Assert.Equal(0.9961841304801171, third.NextDouble());
    }

    [Fact]
    public void SkipToNextSubstreamMovesOneSubstreamLength()
    {
        var stream = new RandomStreamFactory().CreateStream();

        stream.SkipToNextSubstream();

        Assert.Equal(SecondSubstreamOfFirstStream, stream.CurrentState.ToArray());
        Assert.Equal(SecondSubstreamOfFirstStream, stream.SubstreamStartState.ToArray());
        Assert.Equal(0.07939898979733463, stream.NextDouble());
        Assert.Equal(0.4803395047575741, stream.NextDouble());
        Assert.Equal(0.8583222470551328, stream.NextDouble());
    }

    [Fact]
    public void SkipToNextSubstreamAgreesWithJumpingBySeventySixPowersOfTwo()
    {
        var factory = new RandomStreamFactory();
        var byReset = factory.CreateStreamAt(0);
        var byJump = factory.CreateStreamAt(0);

        byReset.SkipToNextSubstream();
        byReset.SkipToNextSubstream();
        byJump.AdvanceByPowerOfTwo(76);
        byJump.AdvanceByPowerOfTwo(76);

        Assert.Equal(byReset.CurrentState, byJump.CurrentState);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(17)]
    public void SkipSubstreamsAgreesWithRepeatedSkipToNextSubstream(int count)
    {
        var factory = new RandomStreamFactory();
        var stepped = factory.CreateStreamAt(0);
        var jumped = factory.CreateStreamAt(0);

        for (var i = 0; i < count; i++)
        {
            stepped.SkipToNextSubstream();
        }

        jumped.SkipSubstreams(count);

        Assert.Equal(stepped.SubstreamStartState, jumped.SubstreamStartState);
        Assert.Equal(stepped.CurrentState, jumped.CurrentState);
        Assert.Equal(count, jumped.SubstreamIndex);
        Assert.Equal(stepped.NextDouble(), jumped.NextDouble());
    }

    [Fact]
    public void SkipSubstreamsByOneMatchesTheHardCodedSecondSubstream()
    {
        var stream = new RandomStreamFactory().CreateStream();

        stream.SkipSubstreams(1);

        Assert.Equal(SecondSubstreamOfFirstStream, stream.CurrentState.ToArray());
        Assert.Equal(SecondSubstreamOfFirstStream, stream.SubstreamStartState.ToArray());
        Assert.Equal(1, stream.SubstreamIndex);
        Assert.Equal(0.07939898979733463, stream.NextDouble());
    }

    [Fact]
    public void SkipSubstreamsOfZeroLeavesTheStreamWhereItIs()
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.SkipToNextSubstream();
        stream.NextDouble();
        var position = stream.CurrentState;

        stream.SkipSubstreams(0);

        // A count of zero is not a rewind: a partly consumed substream stays consumed.
        Assert.Equal(position, stream.CurrentState);
        Assert.NotEqual(stream.SubstreamStartState, stream.CurrentState);
        Assert.Equal(1, stream.SubstreamIndex);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(40)]
    public void NegativeSkipSubstreamsUndoesThePositiveOne(int count)
    {
        var stream = new RandomStreamFactory().CreateStream();
        var start = stream.CurrentState;

        stream.SkipSubstreams(count);
        Assert.NotEqual(start, stream.CurrentState);

        stream.SkipSubstreams(-count);

        Assert.Equal(start, stream.CurrentState);
        Assert.Equal(start, stream.SubstreamStartState);
        Assert.Equal(0, stream.SubstreamIndex);
    }

    [Fact]
    public void SkipSubstreamsLeavesTheStreamStartAlone()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var streamStart = stream.StreamStartState;

        stream.SkipSubstreams(9);

        Assert.Equal(streamStart, stream.StreamStartState);
    }

    [Fact]
    public void SkipSubstreamsAgreesWithAPowerOfTwoJump()
    {
        var factory = new RandomStreamFactory();
        var stepped = factory.CreateStreamAt(0);
        var jumped = factory.CreateStreamAt(0);

        // Two substreams are exactly 2^77 values, which a power-of-two jump reproduces.
        jumped.SkipSubstreams(2);
        stepped.AdvanceByPowerOfTwo(77);

        Assert.Equal(stepped.CurrentState, jumped.CurrentState);
    }

    [Fact]
    public void SkipToSubstreamIsAbsoluteFromTheStreamStart()
    {
        var factory = new RandomStreamFactory();
        var stepped = factory.CreateStreamAt(0);
        var jumped = factory.CreateStreamAt(0);

        for (var i = 0; i < 4; i++)
        {
            stepped.SkipToNextSubstream();
        }

        // Displace the second stream first, to prove the destination does not depend on where it was.
        jumped.SkipSubstreams(9);
        jumped.NextDouble();
        jumped.SkipToSubstream(4);

        Assert.Equal(stepped.SubstreamStartState, jumped.SubstreamStartState);
        Assert.Equal(stepped.CurrentState, jumped.CurrentState);
        Assert.Equal(4, jumped.SubstreamIndex);
    }

    [Fact]
    public void SkipToSubstreamOfZeroReturnsToTheStreamStart()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var expected = Draw(stream, 10);
        stream.SkipSubstreams(6);
        stream.NextDouble();

        stream.SkipToSubstream(0);

        Assert.Equal(expected, Draw(stream, 10));
        Assert.Equal(stream.StreamStartState, stream.SubstreamStartState);
        Assert.Equal(0, stream.SubstreamIndex);
    }

    [Fact]
    public void SubstreamIndexTracksEveryWayOfMoving()
    {
        var stream = new RandomStreamFactory().CreateStream();
        Assert.Equal(0, stream.SubstreamIndex);

        stream.SkipToNextSubstream();
        Assert.Equal(1, stream.SubstreamIndex);

        stream.SkipSubstreams(41);
        Assert.Equal(42, stream.SubstreamIndex);

        stream.SkipSubstreams(-2);
        Assert.Equal(40, stream.SubstreamIndex);

        stream.SkipToSubstream(7);
        Assert.Equal(7, stream.SubstreamIndex);

        // Neither drawing nor the within-substream escape hatches move between substreams.
        stream.NextDouble();
        stream.Advance(1000);
        stream.RewindSubstream();
        Assert.Equal(7, stream.SubstreamIndex);

        stream.RewindStream();
        Assert.Equal(0, stream.SubstreamIndex);
    }

    [Fact]
    public void RewindStreamReplaysTheWholeStream()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var expected = Draw(stream, 10);

        stream.SkipToNextSubstream();
        stream.NextDouble();
        stream.RewindStream();

        Assert.Equal(expected, Draw(stream, 10));
        Assert.Equal(stream.StreamStartState, stream.SubstreamStartState);
    }

    [Fact]
    public void RewindSubstreamReplaysTheCurrentSubstreamOnly()
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.SkipToNextSubstream();
        var expected = Draw(stream, 10);

        stream.RewindSubstream();

        Assert.Equal(expected, Draw(stream, 10));
        Assert.NotEqual(stream.StreamStartState, stream.SubstreamStartState);
    }

    [Fact]
    public void SubstreamsAreContiguousInsideTheStream()
    {
        var factory = new RandomStreamFactory();
        var stepped = factory.CreateStreamAt(0);
        var jumped = factory.CreateStreamAt(0);

        // Two substream jumps land exactly 2^77 steps in, which a power-of-two jump reproduces.
        jumped.SkipToNextSubstream();
        jumped.SkipToNextSubstream();
        stepped.AdvanceByPowerOfTwo(77);

        Assert.Equal(stepped.CurrentState, jumped.CurrentState);
    }

    [Fact]
    public void AdvanceMovesForwardAndBackwardOverTheSameDistance()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var start = stream.CurrentState;

        stream.Advance(1_000_003);
        Assert.NotEqual(start, stream.CurrentState);

        stream.Advance(-1_000_003);
        Assert.Equal(start, stream.CurrentState);
    }

    [Fact]
    public void AdvanceAgreesWithSteppingTheGenerator()
    {
        var factory = new RandomStreamFactory();
        var stepped = factory.CreateStreamAt(0);
        var jumped = factory.CreateStreamAt(0);

        for (var i = 0; i < 5000; i++)
        {
            stepped.NextDouble();
        }

        jumped.Advance(5000);

        Assert.Equal(stepped.CurrentState, jumped.CurrentState);
        Assert.Equal(stepped.NextDouble(), jumped.NextDouble());
    }

    [Fact]
    public void AdvanceLeavesTheStreamAndSubstreamAnchorsAlone()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var streamStart = stream.StreamStartState;
        var substreamStart = stream.SubstreamStartState;

        stream.Advance(12345);

        Assert.Equal(streamStart, stream.StreamStartState);
        Assert.Equal(substreamStart, stream.SubstreamStartState);
    }

    [Fact]
    public void BackwardPowerOfTwoJumpUndoesTheForwardOne()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var start = stream.CurrentState;

        stream.AdvanceByPowerOfTwo(127);
        stream.RetreatByPowerOfTwo(127);

        Assert.Equal(start, stream.CurrentState);
    }

    [Fact]
    public void AdvanceByPowerOfTwoRejectsExponentsOutsideTheSupportedRange()
    {
        var stream = new RandomStreamFactory().CreateStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.AdvanceByPowerOfTwo(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.AdvanceByPowerOfTwo(256));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.RetreatByPowerOfTwo(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.RetreatByPowerOfTwo(256));
    }

    [Fact]
    public void CloneContinuesTheSameSequenceIndependently()
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.SkipSubstreams(3);
        stream.NextDouble();
        stream.Antithetic = true;

        var clone = stream.Clone();
        var fromOriginal = Draw(stream, 5);
        var fromClone = Draw(clone, 5);

        Assert.Equal(fromOriginal, fromClone);
        Assert.True(clone.Antithetic);
        Assert.Equal(stream.SubstreamIndex, clone.SubstreamIndex);

        stream.NextDouble();
        Assert.NotEqual(stream.CurrentState, clone.CurrentState);
    }

    [Fact]
    public void NameDefaultsToEmptyAndIsCarriedThrough()
    {
        var factory = new RandomStreamFactory();

        Assert.Equal(string.Empty, factory.CreateStream().Name);
        Assert.Equal("arrivals", factory.CreateStream("arrivals").Name);
        Assert.Contains("arrivals", factory.CreateStream("arrivals").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SettingANullNameStoresAnEmptyName()
    {
        var stream = new RandomStreamFactory().CreateStream("arrivals");

        stream.Name = null;

        Assert.Equal(string.Empty, stream.Name);
    }

    [Fact]
    public void AsRandomSharesTheStreamState()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var twin = stream.Clone();
        var random = stream.AsRandom();

        Assert.Same(stream, random.Stream);
        Assert.Equal(twin.NextDouble(), random.NextDouble());
        Assert.Equal(twin.CurrentState, stream.CurrentState);
    }

    private static double[] Draw(RandomStream stream, int count)
    {
        var values = new double[count];
        stream.NextDoubles(values);
        return values;
    }
}
