namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Checks the public state value: that every way of building one enforces the seed rules, and that it
/// behaves as a value.
/// </summary>
public class Mrg32k3aStateTests
{
    [Fact]
    public void TheSixValueAndArrayConstructorsAgree()
    {
        var fromValues = new Mrg32k3aState(1, 2, 3, 4, 5, 6);
        var fromArray = new Mrg32k3aState(new uint[] { 1, 2, 3, 4, 5, 6 });

        Assert.Equal(fromValues, fromArray);
        Assert.True(fromValues == fromArray);
        Assert.False(fromValues != fromArray);
        Assert.Equal(fromValues.GetHashCode(), fromArray.GetHashCode());
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 6 }, fromValues.ToArray());
    }

    [Fact]
    public void TheIndexerReadsValuesInStateOrder()
    {
        var state = new Mrg32k3aState(10, 20, 30, 40, 50, 60);

        for (var i = 0; i < Mrg32k3aState.Length; i++)
        {
            Assert.Equal((uint)((i + 1) * 10), state[i]);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => state[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => state[6]);
    }

    [Fact]
    public void DifferentValuesAreNotEqual()
    {
        var state = new Mrg32k3aState(1, 2, 3, 4, 5, 6);

        Assert.NotEqual(state, new Mrg32k3aState(1, 2, 3, 4, 5, 7));
        Assert.True(state != new Mrg32k3aState(6, 5, 4, 3, 2, 1));
        Assert.False(state.Equals((object)"1, 2, 3, 4, 5, 6"));
    }

    [Fact]
    public void TheDefaultSeedIsSixCopiesOf12345()
    {
        Assert.Equal(new uint[] { 12345, 12345, 12345, 12345, 12345, 12345 }, Mrg32k3aState.DefaultSeed.ToArray());
    }

    [Fact]
    public void ToStringListsTheValues()
    {
        Assert.Equal("1, 2, 3, 4, 5, 4294944442", new Mrg32k3aState(1, 2, 3, 4, 5, 4294944442).ToString());
    }

    [Theory]
    [MemberData(nameof(InvalidSeeds))]
    public void AnInvalidSeedIsRefused(uint[] seed)
    {
        Assert.Throws<ArgumentException>(() => new Mrg32k3aState(seed));
        Assert.False(Mrg32k3aState.TryCreate(seed, out var state, out var error));
        Assert.Equal(default, state);
        Assert.False(string.IsNullOrEmpty(error));

        if (seed.Length == Mrg32k3aState.Length)
        {
            Assert.Throws<ArgumentException>(() => new Mrg32k3aState(seed[0], seed[1], seed[2], seed[3], seed[4], seed[5]));
        }
    }

    public static TheoryData<uint[]> InvalidSeeds => new()
    {
        new uint[] { 4294967087, 1, 1, 1, 1, 1 },
        new uint[] { 0, 0, 0, 1, 1, 1 },
        new uint[] { 1, 1, 1, 4294944443, 1, 1 },
        new uint[] { 1, 1, 1, 0, 0, 0 },
        new uint[] { 1, 1, 1, 1, 1, 1, 1 },
        new uint[] { 1, 1, 1, 1, 1 },
    };

    [Fact]
    public void TheLargestValidValuesAreAccepted()
    {
        Assert.True(Mrg32k3aState.TryCreate(new uint[] { 4294967086, 0, 0, 4294944442, 0, 0 }, out var state, out var error));
        Assert.Null(error);
        Assert.Equal(new Mrg32k3aState(4294967086, 0, 0, 4294944442, 0, 0), state);
    }

    [Fact]
    public void ANullArrayIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => new Mrg32k3aState(null!));
        Assert.False(Mrg32k3aState.TryCreate(null, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TheSourceArrayIsCopiedRatherThanAliased()
    {
        var values = new uint[] { 1, 2, 3, 4, 5, 6 };
        var state = new Mrg32k3aState(values);

        values[0] = 999;
        state.ToArray()[1] = 999;

        Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 6 }, state.ToArray());
    }
}
