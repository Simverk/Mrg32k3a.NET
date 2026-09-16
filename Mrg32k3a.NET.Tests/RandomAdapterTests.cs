namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Checks that the framework adapter draws from the stream it wraps and honours the conventions of
/// <see cref="Random"/>.
/// </summary>
public class RandomAdapterTests
{
    [Fact]
    public void AdapterDrawsFromTheStreamItWraps()
    {
        var factory = new RandomStreamFactory();
        var direct = factory.CreateStreamAt(0);
        var wrapped = new StreamBackedRandom(factory.CreateStreamAt(0));

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(direct.NextDouble(), wrapped.NextDouble());
        }
    }

    [Fact]
    public void AdapterAndStreamShareOneState()
    {
        var stream = new RandomStreamFactory().CreateStream();
        var adapter = new StreamBackedRandom(stream);

        Assert.Same(stream, adapter.Stream);

        adapter.NextDouble();
        var afterAdapter = stream.CurrentState;
        stream.NextDouble();

        Assert.NotEqual(afterAdapter, stream.CurrentState);
    }

    [Fact]
    public void AdapterHonoursTheHalfOpenConvention()
    {
        var factory = new RandomStreamFactory();
        var expected = factory.CreateStreamAt(0);
        var adapter = new StreamBackedRandom(factory.CreateStreamAt(0));

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(expected.Next(3, 11), adapter.Next(3, 11));
        }
    }

    [Fact]
    public void AdapterDrawsStayInsideTheirBounds()
    {
        var adapter = new StreamBackedRandom(new RandomStreamFactory().CreateStream());

        for (var i = 0; i < 20_000; i++)
        {
            Assert.InRange(adapter.Next(), 0, int.MaxValue - 1);
            Assert.InRange(adapter.Next(10), 0, 9);
            Assert.InRange(adapter.Next(-5, 5), -5, 4);
            Assert.InRange(adapter.NextDouble(), double.Epsilon, 1.0 - double.Epsilon);
#if !LIBRARY_NETSTANDARD2_0
            Assert.InRange(adapter.NextInt64(100), 0L, 99L);
            Assert.InRange(adapter.NextInt64(-50, 50), -50L, 49L);
            Assert.InRange(adapter.NextSingle(), 0f, MathF.BitDecrement(1f));
#endif
        }
    }

    [Fact]
    public void NextSingleNeverReturnsOne()
    {
        // With x1[n-3] = x1[n-2] = 0 and x2[n-3] = x2[n-1] = 0 both components step to zero, so the
        // combination is m1 and the draw is the largest the stream can produce, m1 * (1 / (m1 + 1)).
        uint[] largestDrawNext = { 0, 0, 1, 0, 1, 0 };
        var stream = RandomStream.FromState(new RandomStreamState
        {
            StreamStart = largestDrawNext,
            SubstreamStart = largestDrawNext,
            Current = largestDrawNext,
        });

        Assert.Equal(4294967087.0 * 2.328306549295727688e-10, stream.Clone().NextDouble());
#if !LIBRARY_NETSTANDARD2_0
        Assert.True(new StreamBackedRandom(stream).NextSingle() < 1f);
#endif
    }

    [Fact]
    public void AdapterFillsByteBuffers()
    {
        var adapter = new StreamBackedRandom(new RandomStreamFactory().CreateStream());
        var buffer = new byte[4096];

        adapter.NextBytes(buffer);

        Assert.Equal(256, buffer.Distinct().Count());
    }

#if !LIBRARY_NETSTANDARD2_0
    [Fact]
    public void AdapterSpanFillMatchesArrayFill()
    {
        var factory = new RandomStreamFactory();
        var viaArray = new StreamBackedRandom(factory.CreateStreamAt(0));
        var viaSpan = new StreamBackedRandom(factory.CreateStreamAt(0));
        var arrayTarget = new byte[128];
        var spanTarget = new byte[128];

        viaArray.NextBytes(arrayTarget);
        viaSpan.NextBytes(spanTarget.AsSpan());

        Assert.Equal(arrayTarget, spanTarget);
    }
#endif

    [Fact]
    public void AdapterRejectsBadArguments()
    {
        var adapter = new StreamBackedRandom(new RandomStreamFactory().CreateStream());

        Assert.Throws<ArgumentNullException>(() => new StreamBackedRandom(null!));
        Assert.Throws<ArgumentNullException>(() => adapter.NextBytes(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => adapter.Next(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => adapter.Next(5, 4));
#if !LIBRARY_NETSTANDARD2_0
        Assert.Throws<ArgumentOutOfRangeException>(() => adapter.NextInt64(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => adapter.NextInt64(5, 4));
#endif
    }

    [Fact]
    public void AdapterCanStandInForTheFrameworkType()
    {
        Random random = new StreamBackedRandom(new RandomStreamFactory().CreateStream());
        var items = Enumerable.Range(0, 20).ToArray();

        var shuffled = items.OrderBy(_ => random.Next()).ToArray();

        Assert.Equal(items.OrderBy(x => x), shuffled.OrderBy(x => x));
    }
}
