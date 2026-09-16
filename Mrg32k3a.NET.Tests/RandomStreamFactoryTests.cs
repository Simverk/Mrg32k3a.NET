namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Checks seed handling and creation order, including the direct indexing that lets a worker build
/// its own stream without creating the ones before it.
/// </summary>
public class RandomStreamFactoryTests
{
    [Fact]
    public void TwoFactoriesWithTheSameSeedAreIndependentButIdentical()
    {
        var first = new RandomStreamFactory();
        var second = new RandomStreamFactory();

        first.CreateStream();
        first.CreateStream();

        Assert.Equal(first.CreateStream().StreamStartState, second.CreateStreamAt(2).StreamStartState);
        Assert.Equal(3L, first.CreatedStreamCount);
        Assert.Equal(0L, second.CreatedStreamCount);
    }

    [Fact]
    public void DirectIndexingMatchesSequentialCreation()
    {
        var sequential = new RandomStreamFactory();
        var indexed = new RandomStreamFactory();

        for (var i = 0; i < 12; i++)
        {
            Assert.Equal(
                sequential.CreateStream().StreamStartState,
                indexed.CreateStreamAt(i).StreamStartState);
        }

        Assert.Equal(0L, indexed.CreatedStreamCount);
        Assert.Equal(12L, sequential.CreatedStreamCount);
    }

    [Fact]
    public void DirectIndexingReachesDistantStreamsCheaply()
    {
        var factory = new RandomStreamFactory();
        var far = factory.CreateStreamAt(1_000_000);
        var reference = factory.CreateStreamAt(999_999);

        reference.AdvanceByPowerOfTwo(127);

        Assert.Equal(reference.CurrentState, far.CurrentState);
    }

    [Fact]
    public void ANegativeStreamIndexIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RandomStreamFactory().CreateStreamAt(-1));
    }

    [Fact]
    public void AnExplicitSeedBecomesTheFirstStreamStart()
    {
        var seed = new Mrg32k3aState(1, 2, 3, 4, 5, 6);
        var factory = new RandomStreamFactory(seed);

        Assert.Equal(seed, factory.Seed);
        Assert.Equal(seed, factory.CreateStream().StreamStartState);
    }

    [Fact]
    public void TheParameterlessFactoryUsesTheDefaultSeed()
    {
        Assert.Equal(Mrg32k3aState.DefaultSeed, new RandomStreamFactory().Seed);
    }

    [Fact]
    public void AnUninitialisedSeedIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new RandomStreamFactory(default));
    }

    [Fact]
    public void CreatingStreamsLeavesTheSeedAlone()
    {
        var factory = new RandomStreamFactory();

        factory.CreateStreams(3);

        Assert.Equal(Mrg32k3aState.DefaultSeed, factory.Seed);
    }

    [Fact]
    public void BatchCreationMatchesSequentialCreation()
    {
        var batched = new RandomStreamFactory();
        var sequential = new RandomStreamFactory();
        batched.CreateStream();
        sequential.CreateStream();

        var streams = batched.CreateStreams(5);

        Assert.Equal(5, streams.Length);
        foreach (var stream in streams)
        {
            Assert.Equal(sequential.CreateStream().StreamStartState, stream.StreamStartState);
            Assert.Equal(string.Empty, stream.Name);
        }

        Assert.Equal(6L, batched.CreatedStreamCount);
        Assert.Equal(sequential.CreateStream().StreamStartState, batched.CreateStream().StreamStartState);
    }

    [Fact]
    public void BatchCreationAcceptsZeroAndRefusesNegativeCounts()
    {
        var factory = new RandomStreamFactory();

        Assert.Empty(factory.CreateStreams(0));
        Assert.Equal(0L, factory.CreatedStreamCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => factory.CreateStreams(-1));
    }

    [Fact]
    public void ConcurrentBatchesAreEachConsecutive()
    {
        var factory = new RandomStreamFactory();
        var batches = new RandomStream[16][];

        Parallel.For(0, batches.Length, i => batches[i] = factory.CreateStreams(8));

        foreach (var batch in batches)
        {
            var reference = RandomStream.FromState(batch[0].SaveState());
            for (var i = 1; i < batch.Length; i++)
            {
                reference.AdvanceByPowerOfTwo(127);
                Assert.Equal(reference.CurrentState, batch[i].StreamStartState);
            }
        }

        Assert.Equal(16L * 8, factory.CreatedStreamCount);
    }

    [Fact]
    public void ConcurrentCreationHandsOutDistinctStreams()
    {
        var factory = new RandomStreamFactory();
        var streams = new RandomStream[64];

        Parallel.For(0, streams.Length, i => streams[i] = factory.CreateStream());

        var starts = streams.Select(s => s.StreamStartState).ToHashSet();

        Assert.Equal(streams.Length, starts.Count);
        Assert.Equal(streams.Length, (int)factory.CreatedStreamCount);
    }
}
