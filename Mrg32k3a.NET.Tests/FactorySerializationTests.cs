using System.Text.Json;

namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Checks that a factory can be captured, moved through a serializer and resumed without handing out
/// streams it has already handed out, and that malformed snapshots are refused rather than silently
/// accepted.
/// </summary>
/// <remarks>
/// The creation order is the part that matters. A stream snapshot preserves where a stream is; only
/// a factory snapshot preserves which streams have been taken, and without it a reloaded run issues
/// live streams a second time and two workers draw the same values.
/// </remarks>
public class FactorySerializationTests
{
    [Fact]
    public void ARestoredFactoryDoesNotReissueStreamsThatAreAlreadyLive()
    {
        var original = new RandomStreamFactory();
        var live = original.CreateStreams(5).Select(s => s.StreamStartState).ToList();

        var resumed = RandomStreamFactory.FromState(original.SaveState());
        var next = resumed.CreateStream().StreamStartState;

        Assert.DoesNotContain(next, live);
        Assert.Equal(original.CreateStream().StreamStartState, next);
        Assert.Equal(5L, resumed.CreatedStreamCount - 1);
    }

    [Fact]
    public void TheCreationOrderSurvivesASaveAndLoadRoundTrip()
    {
        var original = new RandomStreamFactory();
        original.CreateStreams(7);

        var json = JsonSerializer.Serialize(original.SaveState());
        var resumed = RandomStreamFactory.FromState(
            JsonSerializer.Deserialize<RandomStreamFactoryState>(json)!);

        Assert.Equal(original.Seed, resumed.Seed);
        Assert.Equal(original.CreatedStreamCount, resumed.CreatedStreamCount);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(original.CreateStream().StreamStartState, resumed.CreateStream().StreamStartState);
        }
    }

    [Fact]
    public void SnapshotCarriesTheSeedAndTheCount()
    {
        var seed = new Mrg32k3aState(1, 2, 3, 4, 5, 6);
        var factory = new RandomStreamFactory(seed);
        factory.CreateStreams(4);

        var snapshot = factory.SaveState();

        Assert.Equal(RandomStreamFactoryState.CurrentVersion, snapshot.Version);
        Assert.Equal(seed.ToArray(), snapshot.Seed);
        Assert.Equal(4L, snapshot.CreatedStreamCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(1_000_000)]
    public void RestoringAgreesWithCreateStreamAt(long count)
    {
        var reference = new RandomStreamFactory();
        var snapshot = new RandomStreamFactoryState
        {
            Seed = reference.Seed.ToArray(),
            CreatedStreamCount = count,
        };

        var resumed = RandomStreamFactory.FromState(snapshot);

        Assert.Equal(
            reference.CreateStreamAt(count).StreamStartState,
            resumed.CreateStream().StreamStartState);
    }

    [Fact]
    public void ARestoredFactoryKeepsAnExplicitSeed()
    {
        var seed = new Mrg32k3aState(99, 98, 97, 96, 95, 94);
        var original = new RandomStreamFactory(seed);
        original.CreateStream();

        var resumed = RandomStreamFactory.FromState(original.SaveState());

        Assert.Equal(seed, resumed.Seed);
        Assert.Equal(seed, resumed.CreateStreamAt(0).StreamStartState);
    }

    [Fact]
    public void ASnapshotTakenBeforeAnyStreamRestoresAnUntouchedFactory()
    {
        var resumed = RandomStreamFactory.FromState(new RandomStreamFactory().SaveState());

        Assert.Equal(0L, resumed.CreatedStreamCount);
        Assert.Equal(Mrg32k3aState.DefaultSeed, resumed.Seed);
        Assert.Equal(Mrg32k3aState.DefaultSeed, resumed.CreateStream().StreamStartState);
    }

    [Fact]
    public void ASnapshotTakenDuringConcurrentCreationIsCoherent()
    {
        var factory = new RandomStreamFactory();
        var snapshots = new RandomStreamFactoryState[32];

        Parallel.For(0, 64, i =>
        {
            factory.CreateStream();
            if (i % 2 == 0)
            {
                snapshots[i / 2] = factory.SaveState();
            }
        });

        foreach (var snapshot in snapshots)
        {
            // Every snapshot must name a position the factory really passed through, and restoring
            // from it must land on exactly that position rather than somewhere between two of them.
            Assert.InRange(snapshot.CreatedStreamCount, 1L, 64L);
            Assert.Equal(factory.Seed.ToArray(), snapshot.Seed);
            Assert.Equal(
                factory.CreateStreamAt(snapshot.CreatedStreamCount).StreamStartState,
                RandomStreamFactory.FromState(snapshot).CreateStream().StreamStartState);
        }
    }

    [Fact]
    public void AnUnknownVersionIsRefused()
    {
        var snapshot = new RandomStreamFactory().SaveState();
        snapshot.Version = 99;

        var error = Assert.Throws<ArgumentException>(() => RandomStreamFactory.FromState(snapshot));
        Assert.Contains("99", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(SerializationTests.InvalidStates), MemberType = typeof(SerializationTests))]
    public void AnInvalidSeedIsRefused(uint[] invalid, string expectedFragment)
    {
        var snapshot = new RandomStreamFactory().SaveState();
        snapshot.Seed = invalid;

        var error = Assert.Throws<ArgumentException>(() => RandomStreamFactory.FromState(snapshot));
        Assert.Contains("Seed", error.Message, StringComparison.Ordinal);
        Assert.Contains(expectedFragment, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullSeedIsRefused()
    {
        var snapshot = new RandomStreamFactory().SaveState();
        snapshot.Seed = null!;

        var error = Assert.Throws<ArgumentException>(() => RandomStreamFactory.FromState(snapshot));
        Assert.Contains("Seed", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void ANegativeCreatedStreamCountIsRefused(long count)
    {
        var snapshot = new RandomStreamFactory().SaveState();
        snapshot.CreatedStreamCount = count;

        var error = Assert.Throws<ArgumentException>(() => RandomStreamFactory.FromState(snapshot));
        Assert.Contains("CreatedStreamCount", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullSnapshotIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => RandomStreamFactory.FromState(null!));
    }

    [Fact]
    public void AWholeRunResumesFromOneCheckpointAndProducesTheSameValues()
    {
        var (factory, streams) = BuildAndWarm();

        var checkpoint = JsonSerializer.Deserialize<CheckpointDocument>(
            JsonSerializer.Serialize(new CheckpointDocument
            {
                Factory = factory.SaveState(),
                Streams = streams.Select(s => s.SaveState()).ToList(),
            }))!;

        var fromOriginal = Continue(factory, streams);
        var fromCheckpoint = Continue(
            RandomStreamFactory.FromState(checkpoint.Factory),
            checkpoint.Streams.Select(s => RandomStream.FromState(s)).ToArray());

        Assert.Equal(fromOriginal, fromCheckpoint);
    }

    /// <summary>Builds a run of three streams and takes it part way, so the checkpoint is mid-run.</summary>
    private static (RandomStreamFactory Factory, RandomStream[] Streams) BuildAndWarm()
    {
        var factory = new RandomStreamFactory(new Mrg32k3aState(11111, 22222, 33333, 44444, 55555, 66666));
        var streams = new[]
        {
            factory.CreateStream("arrivals"),
            factory.CreateStream("service"),
            factory.CreateStream("routing"),
        };
        streams[1].HighPrecision = true;
        streams[2].Antithetic = true;

        for (var replication = 0; replication < 3; replication++)
        {
            foreach (var stream in streams)
            {
                stream.NextDoubles(new double[7]);
                stream.SkipToNextSubstream();
            }
        }

        return (factory, streams);
    }

    /// <summary>
    /// The workload both the original run and the resumed one perform, recorded value by value.
    /// </summary>
    /// <remarks>
    /// Anything a future field is added to has to show up here, or the comparison cannot notice that
    /// the field was left out of the snapshot: the draws cover both output flags, the jumps are
    /// relative as well as absolute so they depend on the restored substream index rather than only
    /// on the state vectors, the adapter is included because it shares the stream, and
    /// <c>ToDetailedString</c> is recorded because it prints every anchor at once.
    /// </remarks>
    private static List<string> Continue(RandomStreamFactory factory, RandomStream[] streams)
    {
        var log = new List<string>();
        for (var replication = 0; replication < 6; replication++)
        {
            for (var i = 0; i < streams.Length; i++)
            {
                var stream = streams[i];
                var draws = 3 + i + (replication % 3);
                for (var d = 0; d < draws; d++)
                {
                    log.Add(FormattableString.Invariant($"u:{BitConverter.DoubleToInt64Bits(stream.NextDouble())}"));
                    log.Add(FormattableString.Invariant($"i:{stream.NextInt32Inclusive(1, 6)}"));
                }

                log.Add(FormattableString.Invariant($"r:{stream.AsRandom().Next(1000)}"));
            }

            foreach (var stream in streams)
            {
                stream.SkipToNextSubstream();
            }
        }

        streams[0].SkipSubstreams(25);
        streams[1].SkipToSubstream(4);
        streams[2].SkipSubstreams(-2);
        streams[2].RewindSubstream();

        foreach (var stream in streams)
        {
            log.Add(FormattableString.Invariant($"n:{stream.SubstreamIndex}"));
            log.Add("d:" + stream.ToDetailedString());
            log.Add(FormattableString.Invariant($"u:{BitConverter.DoubleToInt64Bits(stream.NextDouble())}"));
        }

        var extra = factory.CreateStream("extra");
        log.Add("d:" + extra.ToDetailedString());
        log.Add(FormattableString.Invariant($"c:{factory.CreatedStreamCount}"));
        return log;
    }

    /// <summary>A whole run's checkpoint: the factory, and every stream it has handed out.</summary>
    public sealed class CheckpointDocument
    {
        /// <summary>Gets or sets the factory snapshot.</summary>
        public RandomStreamFactoryState Factory { get; set; } = new RandomStreamFactoryState();

        /// <summary>Gets or sets the snapshot of each live stream.</summary>
        public List<RandomStreamState> Streams { get; set; } = new List<RandomStreamState>();
    }
}
