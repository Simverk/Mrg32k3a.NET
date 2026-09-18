namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Checks the one invariant the substream operations exist to protect: a stream never leaves its own
/// block of 2^127 values, so it can never collide with the stream that follows it.
/// </summary>
/// <remarks>
/// Walking to the last substream would take 2^51 calls, which is why these cases only became
/// testable once a stream could be put on a given substream directly. Every rejection is checked to
/// leave the stream exactly where it was, so a caught exception never hides a half-applied jump.
/// </remarks>
public class SubstreamBoundaryTests
{
    private const long SubstreamsPerStream = 1L << 51;
    private const long LastSubstream = SubstreamsPerStream - 1;

    public static TheoryData<long> IndicesOutsideTheStream => new()
    {
        -1, long.MinValue, SubstreamsPerStream, SubstreamsPerStream + 1, long.MaxValue,
    };

    [Theory]
    [MemberData(nameof(IndicesOutsideTheStream))]
    public void SkipToSubstreamRejectsIndicesOutsideTheStream(long index)
    {
        var stream = new RandomStreamFactory().CreateStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.SkipToSubstream(index));
    }

    [Fact]
    public void SkipToSubstreamAcceptsTheLastSubstream()
    {
        var stream = new RandomStreamFactory().CreateStream();

        stream.SkipToSubstream(LastSubstream);

        Assert.Equal(LastSubstream, stream.SubstreamIndex);
        Assert.Equal(stream.SubstreamStartState, stream.CurrentState);
    }

    [Theory]
    [InlineData(SubstreamsPerStream)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void SkipSubstreamsFromTheStartRejectsCountsThatWouldLeaveTheBlock(long count)
    {
        var stream = new RandomStreamFactory().CreateStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.SkipSubstreams(count));
    }

    [Fact]
    public void SkipSubstreamsFromTheLastSubstreamRefusesToGoFurther()
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.SkipToSubstream(LastSubstream);

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.SkipSubstreams(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.SkipSubstreams(long.MaxValue));
    }

    [Fact]
    public void SkipSubstreamsSpansTheWholeStreamInBothDirections()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var start = stream.CurrentState;

        stream.SkipSubstreams(LastSubstream);
        Assert.Equal(LastSubstream, stream.SubstreamIndex);

        stream.SkipSubstreams(-LastSubstream);

        Assert.Equal(0, stream.SubstreamIndex);
        Assert.Equal(start, stream.CurrentState);
    }

    [Fact]
    public void TheLastSubstreamOfAStreamStopsShortOfTheNextStream()
    {
        var factory = new RandomStreamFactory();
        var first = factory.CreateStream();
        var second = factory.CreateStream();

        first.SkipToSubstream(LastSubstream);

        // One more substream would land exactly on the next stream's start, which is the collision
        // the guard exists to prevent.
        Assert.NotEqual(second.StreamStartState, first.SubstreamStartState);
        Assert.Throws<InvalidOperationException>(first.SkipToNextSubstream);
    }

    [Fact]
    public void SkipToNextSubstreamRefusesToLeaveTheLastSubstream()
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.SkipToSubstream(LastSubstream);

        var error = Assert.Throws<InvalidOperationException>(stream.SkipToNextSubstream);

        Assert.Contains("last", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARejectedSkipLeavesTheStreamExactlyWhereItWas()
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.SkipToSubstream(12);
        stream.NextDouble();
        var streamStart = stream.StreamStartState;
        var substreamStart = stream.SubstreamStartState;
        var current = stream.CurrentState;

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.SkipSubstreams(SubstreamsPerStream));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.SkipSubstreams(-13));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.SkipToSubstream(-1));

        Assert.Equal(streamStart, stream.StreamStartState);
        Assert.Equal(substreamStart, stream.SubstreamStartState);
        Assert.Equal(current, stream.CurrentState);
        Assert.Equal(12, stream.SubstreamIndex);
    }

    [Fact]
    public void TheExactEdgesOfTheCountRangeAreAccepted()
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.SkipToSubstream(12);

        stream.SkipSubstreams(-12);
        Assert.Equal(0, stream.SubstreamIndex);

        stream.SkipSubstreams(LastSubstream);
        Assert.Equal(LastSubstream, stream.SubstreamIndex);

        stream.SkipSubstreams(0);
        Assert.Equal(LastSubstream, stream.SubstreamIndex);
    }
}
