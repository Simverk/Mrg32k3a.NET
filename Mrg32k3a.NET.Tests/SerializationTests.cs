using System.Text.Json;

namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Checks that a stream can be captured, moved through a serializer and resumed, and that malformed
/// snapshots are refused rather than silently accepted.
/// </summary>
public class SerializationTests
{
    [Fact]
    public void SnapshotAndRestoreContinueTheSameSequence()
    {
        var stream = new RandomStreamFactory().CreateStream("worker-3");
        stream.HighPrecision = true;
        stream.SkipToNextSubstream();
        for (var i = 0; i < 17; i++)
        {
            stream.NextDouble();
        }

        var snapshot = stream.SaveState();
        var expected = Draw(stream, 20);
        var resumed = RandomStream.FromState(snapshot);

        Assert.Equal(expected, Draw(resumed, 20));
        Assert.Equal("worker-3", resumed.Name);
        Assert.True(resumed.HighPrecision);
    }

    [Fact]
    public void SnapshotSurvivesAJsonRoundTrip()
    {
        var stream = new RandomStreamFactory().CreateStream("json");
        stream.Antithetic = true;
        stream.SkipToNextSubstream();
        stream.NextDouble();

        var json = JsonSerializer.Serialize(stream.SaveState());
        var restored = RandomStream.FromState(JsonSerializer.Deserialize<RandomStreamState>(json)!);

        Assert.Equal(Draw(stream, 25), Draw(restored, 25));
        Assert.Equal(stream.StreamStartState, restored.StreamStartState);
        Assert.Equal(stream.SubstreamStartState, restored.SubstreamStartState);
        Assert.True(restored.Antithetic);
    }

    [Fact]
    public void SnapshotCarriesAllThreeAnchors()
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.SkipToNextSubstream();
        stream.NextDouble();

        var snapshot = stream.SaveState();

        Assert.Equal(RandomStreamState.CurrentVersion, snapshot.Version);
        Assert.Equal(stream.StreamStartState.ToArray(), snapshot.StreamStart);
        Assert.Equal(stream.SubstreamStartState.ToArray(), snapshot.SubstreamStart);
        Assert.Equal(stream.CurrentState.ToArray(), snapshot.Current);
    }

    [Fact]
    public void RestoringKeepsTheResetOperationsMeaningful()
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.SkipToNextSubstream();
        var substreamValues = Draw(stream, 10);

        var restored = RandomStream.FromState(stream.SaveState());
        restored.RewindSubstream();

        Assert.Equal(substreamValues, Draw(restored, 10));
    }

    [Fact]
    public void AnExplicitNameOverridesTheOneInTheSnapshot()
    {
        var snapshot = new RandomStreamFactory().CreateStream("original").SaveState();

        Assert.Equal("renamed", RandomStream.FromState(snapshot, "renamed").Name);
    }

    [Fact]
    public void AnUnknownVersionIsRefused()
    {
        var snapshot = new RandomStreamFactory().CreateStream().SaveState();
        snapshot.Version = 99;

        var error = Assert.Throws<ArgumentException>(() => RandomStream.FromState(snapshot));
        Assert.Contains("99", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidStates))]
    public void AnInvalidStateVectorIsRefused(uint[] invalid, string expectedFragment)
    {
        var snapshot = new RandomStreamFactory().CreateStream().SaveState();
        snapshot.Current = invalid;

        var error = Assert.Throws<ArgumentException>(() => RandomStream.FromState(snapshot));
        Assert.Contains(expectedFragment, error.Message, StringComparison.Ordinal);
    }

    public static TheoryData<uint[], string> InvalidStates => new()
    {
        { new uint[] { 4294967087, 1, 1, 1, 1, 1 }, "4294967087" },
        { new uint[] { 0, 0, 0, 1, 1, 1 }, "first three" },
        { new uint[] { 1, 1, 1, 4294944443, 1, 1 }, "4294944443" },
        { new uint[] { 1, 1, 1, 0, 0, 0 }, "last three" },
        { new uint[] { 1, 1, 1, 1, 1 }, "exactly six" },
    };

    [Fact]
    public void ANullSnapshotIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => RandomStream.FromState(null!));
        Assert.Throws<ArgumentNullException>(() => new RandomStreamFactory().CreateStream().LoadState(null!));
    }

    private static double[] Draw(RandomStream stream, int count)
    {
        var values = new double[count];
        stream.NextDoubles(values);
        return values;
    }
}
