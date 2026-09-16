namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Checks the integer draws: the closed-range form of RandInt in L'Ecuyer et al. (2002) and the
/// half-open form that follows the framework convention.
/// </summary>
public class IntegerDrawTests
{
    [Fact]
    public void ClosedRangeDrawsMatchTheScaledUniformValues()
    {
        var stream = new RandomStreamFactory().CreateStream();

        Assert.Equal(new[] { 2, 4, 4, 9, 3, 6 }, Draw(stream, 1, 10, 6));
    }

    [Fact]
    public void ClosedRangeDrawUsesExactlyOneUniformValue()
    {
        var factory = new RandomStreamFactory();
        var integers = factory.CreateStreamAt(0);
        var doubles = factory.CreateStreamAt(0);

        for (var i = 0; i < 1000; i++)
        {
            var expected = 1 + (int)(10 * doubles.NextDouble());
            Assert.Equal(expected, integers.NextInt32Inclusive(1, 10));
        }

        Assert.Equal(doubles.CurrentState, integers.CurrentState);
    }

    [Fact]
    public void ClosedRangeDrawsStayInsideTheirBounds()
    {
        var stream = new RandomStreamFactory().CreateStream();

        for (var i = 0; i < 100_000; i++)
        {
            Assert.InRange(stream.NextInt32Inclusive(-3, 7), -3, 7);
        }
    }

    [Fact]
    public void ClosedRangeDrawsStayInsideTheirBoundsInHighPrecisionMode()
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.HighPrecision = true;

        for (var i = 0; i < 100_000; i++)
        {
            Assert.InRange(stream.NextInt64Inclusive(0, 9), 0L, 9L);
        }
    }

    [Fact]
    public void SingleValueRangeAlwaysReturnsThatValue()
    {
        var stream = new RandomStreamFactory().CreateStream();

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(42, stream.NextInt32Inclusive(42, 42));
            Assert.Equal(42L, stream.NextInt64Inclusive(42, 42));
        }
    }

    [Fact]
    public void HalfOpenDrawAgreesWithTheClosedRangeOneStepShorter()
    {
        var factory = new RandomStreamFactory();
        var halfOpen = factory.CreateStreamAt(0);
        var closed = factory.CreateStreamAt(0);

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(closed.NextInt32Inclusive(5, 19), halfOpen.Next(5, 20));
        }
    }

    [Fact]
    public void HalfOpenDrawReturnsTheLowerBoundForAnEmptyRange()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var before = stream.CurrentState;

        Assert.Equal(7, stream.Next(7, 7));
        Assert.Equal(before, stream.CurrentState);
    }

    [Fact]
    public void SixtyFourBitDrawsCoverAWideRange()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var seenLow = false;
        var seenHigh = false;

        for (var i = 0; i < 10_000; i++)
        {
            var value = stream.NextInt64Inclusive(0, long.MaxValue - 1);
            Assert.InRange(value, 0L, long.MaxValue - 1);
            seenLow |= value < long.MaxValue / 4;
            seenHigh |= value > long.MaxValue / 4 * 3;
        }

        Assert.True(seenLow && seenHigh, "the draws did not cover both ends of the range");
    }

    [Fact]
    public void InvertedBoundsAreRejected()
    {
        var stream = new RandomStreamFactory().CreateStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextInt32Inclusive(5, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextInt64Inclusive(5, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Next(5, 4));
    }

    [Fact]
    public void ARangeTooWideToCountIsRejected()
    {
        var stream = new RandomStreamFactory().CreateStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextInt64Inclusive(long.MinValue, long.MaxValue));
    }

    [Fact]
    public void ClosedRangeDrawsAreRoughlyUniform()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var counts = new int[10];

        for (var i = 0; i < 1_000_000; i++)
        {
            counts[stream.NextInt32Inclusive(0, 9)]++;
        }

        foreach (var count in counts)
        {
            Assert.InRange(count, 98_000, 102_000);
        }
    }

    [Fact]
    public void BulkIntegerFillMatchesRepeatedDraws()
    {
        var factory = new RandomStreamFactory();
        var bulk = factory.CreateStreamAt(0);
        var single = factory.CreateStreamAt(0);
        var buffer = new int[64];

        bulk.NextInt32sInclusive(1, 6, buffer, 0, buffer.Length);

        for (var i = 0; i < buffer.Length; i++)
        {
            Assert.Equal(single.NextInt32Inclusive(1, 6), buffer[i]);
        }
    }

    [Fact]
    public void ExclusiveUpperBoundsMatchTheInclusiveDrawOneBelow()
    {
        var factory = new RandomStreamFactory();
        var exclusive = factory.CreateStreamAt(0);
        var inclusive = factory.CreateStreamAt(0);

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(inclusive.NextInt32Inclusive(0, 9), exclusive.Next(10));
            Assert.Equal(inclusive.NextInt64Inclusive(0, 99), exclusive.NextInt64(100));
            Assert.Equal(inclusive.NextInt64Inclusive(-50, 49), exclusive.NextInt64(-50, 50));
        }

        Assert.Equal(inclusive.CurrentState, exclusive.CurrentState);
    }

    [Fact]
    public void EmptyExclusiveRangesReturnTheLowerBoundWithoutDrawing()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var before = stream.CurrentState;

        Assert.Equal(0, stream.Next(0));
        Assert.Equal(0L, stream.NextInt64(0));
        Assert.Equal(7L, stream.NextInt64(7, 7));
        Assert.Equal(before, stream.CurrentState);
    }

    [Fact]
    public void ExclusiveDrawsRejectBadBounds()
    {
        var stream = new RandomStreamFactory().CreateStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Next(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextInt64(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextInt64(5, 4));
    }

#if !LIBRARY_NETSTANDARD2_0
    [Fact]
    public void TheAdapterForwardsToTheExclusiveDraws()
    {
        var factory = new RandomStreamFactory();
        var direct = factory.CreateStreamAt(0);
        var adapter = factory.CreateStreamAt(0).AsRandom();

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(direct.Next(int.MaxValue), adapter.Next());
            Assert.Equal(direct.Next(17), adapter.Next(17));
            Assert.Equal(direct.NextInt64(long.MaxValue), adapter.NextInt64());
            Assert.Equal(direct.NextInt64(1_000_000_007L), adapter.NextInt64(1_000_000_007L));
            Assert.Equal(direct.NextInt64(-3L, 3L), adapter.NextInt64(-3L, 3L));
        }
    }
#endif

    [Fact]
    public void BulkIntegerArrayFillRejectsInvertedBoundsEvenWhenThereIsNothingToFill()
    {
        var stream = new RandomStreamFactory().CreateStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextInt32sInclusive(6, 1, Array.Empty<int>(), 0, 0));
    }

#if !LIBRARY_NETSTANDARD2_0
    [Fact]
    public void BulkIntegerSpanFillRejectsInvertedBoundsEvenWhenThereIsNothingToFill()
    {
        var stream = new RandomStreamFactory().CreateStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextInt32sInclusive(6, 1, Span<int>.Empty));
    }
#endif

    [Fact]
    public void BulkFillRejectsRangesOutsideTheBuffer()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var buffer = new double[4];

        Assert.Throws<ArgumentNullException>(() => stream.NextDoubles(null!, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextDoubles(buffer, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextDoubles(buffer, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.NextDoubles(buffer, 2, 3));
    }

#if !LIBRARY_NETSTANDARD2_0
    [Fact]
    public void SpanFillMatchesArrayFill()
    {
        var factory = new RandomStreamFactory();
        var viaSpan = factory.CreateStreamAt(0);
        var viaArray = factory.CreateStreamAt(0);
        var spanTarget = new double[32];
        var arrayTarget = new double[32];

        viaSpan.NextDoubles(spanTarget.AsSpan());
        viaArray.NextDoubles(arrayTarget, 0, arrayTarget.Length);

        Assert.Equal(arrayTarget, spanTarget);
    }
#endif

    private static int[] Draw(RandomStream stream, int min, int max, int count)
    {
        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = stream.NextInt32Inclusive(min, max);
        }

        return values;
    }
}
