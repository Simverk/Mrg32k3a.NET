namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Compares this implementation against verification vectors produced by the RngStreams C reference,
/// draw for draw and state for state.
/// </summary>
/// <remarks>
/// Uniform draws are compared by exact IEEE-754 bit pattern, not by tolerance, and every draw must
/// also leave the stream in the state the reference recorded. A single differing bit anywhere fails
/// the run.
/// </remarks>
public class ReferenceVectorTests
{
    private static readonly uint[] DefaultSeed = { 12345, 12345, 12345, 12345, 12345, 12345 };
    private static readonly uint[] AllOnesSeed = { 1, 1, 1, 1, 1, 1 };

    /// <summary>
    /// Cases this suite deliberately does not reproduce, with the reason. The accounting test below
    /// fails if the vector file ever grows a case that is neither covered nor listed here. It is
    /// empty: since the driver trace arrived, every case in the vector file is reproduced.
    /// </summary>
    private static readonly Dictionary<string, string> NotReproduced = new();

    public static TheoryData<string> UniformSequenceCases => ToTheoryData("u01_sequence");

    public static TheoryData<string> IntegerSequenceCases => ToTheoryData("randint_sequence");

    [Fact]
    public void TheVectorArchiveMatchesItsPublishedHashes()
    {
        var file = ReferenceVectors.File;

        Assert.True(ReferenceVectors.UsingShippedArchive || HasOverride(), "no vector source was loaded");
        Assert.Contains("MRG32k3a", file.Meta.Generator, StringComparison.Ordinal);
        Assert.NotEmpty(file.Cases);
    }

    [Theory]
    [MemberData(nameof(UniformSequenceCases))]
    public void UniformDrawsMatchTheReferenceBitForBit(string name)
    {
        var reference = ReferenceVectors.Case(name);
        var stream = StreamPositionedAt(reference.InitialCg!);
        stream.Antithetic = reference.Antithetic == 1;
        stream.HighPrecision = reference.HighPrecision == 1;

        foreach (var draw in reference.Draws!)
        {
            var actual = stream.NextDouble();

            AssertSameBits(draw.U01Bits!, actual, $"{name} draw {draw.Index}");
            Assert.Equal(draw.U01!.Value, actual);
            Assert.Equal(draw.Cg, stream.CurrentState.ToArray());
        }
    }

    [Theory]
    [MemberData(nameof(IntegerSequenceCases))]
    public void IntegerDrawsMatchTheReference(string name)
    {
        var reference = ReferenceVectors.Case(name);
        var low = reference.Low!.Value;
        var high = reference.High!.Value;

        var wide = StreamPositionedAt(reference.InitialCg!);
        var narrow = StreamPositionedAt(reference.InitialCg!);
        wide.Antithetic = reference.Antithetic == 1;
        narrow.Antithetic = reference.Antithetic == 1;
        wide.HighPrecision = reference.HighPrecision == 1;
        narrow.HighPrecision = reference.HighPrecision == 1;

        foreach (var draw in reference.Draws!)
        {
            var actual = wide.NextInt64Inclusive(low, high);

            Assert.Equal(draw.Value!.Value, actual);
            Assert.Equal(draw.Cg, wide.CurrentState.ToArray());

            // The 32-bit entry point must agree with the 64-bit one on the same range.
            Assert.Equal(actual, narrow.NextInt32Inclusive((int)low, (int)high));
        }
    }

    [Fact]
    public void TheFirstThirtyFiveIntegerDrawsSumToTheDocumentedTotal()
    {
        // A quick cross-check on the first thirty-five draws of the reference case.
        var reference = ReferenceVectors.Case("randint_1_10_n");
        var stream = StreamPositionedAt(reference.InitialCg!);
        var total = 0L;

        for (var i = 0; i < 35; i++)
        {
            total += stream.NextInt32Inclusive(1, 10);
        }

        Assert.Equal(186L, total);
    }

    [Fact]
    public void StreamStartsMatchTheReference()
    {
        var factory = new RandomStreamFactory();
        var first = factory.CreateStream();
        var second = factory.CreateStream();
        var third = factory.CreateStream();

        Assert.Equal(ExpectedState("initial_s0_is_default_seed"), first.StreamStartState.ToArray());
        Assert.Equal(ExpectedState("initial_s1_known"), second.StreamStartState.ToArray());
        Assert.Equal(ExpectedState("initial_s2_known"), third.StreamStartState.ToArray());
    }

    [Fact]
    public void StreamSpacingMatchesTheReference()
    {
        var stream = new RandomStreamFactory().CreateStream();

        stream.AdvanceByPowerOfTwo(127);

        Assert.Equal(ExpectedState("spacing_s0_plus_2pow127_eq_s1"), stream.CurrentState.ToArray());
    }

    [Fact]
    public void ThirtyFiveStepAdvanceMatchesTheReferenceByEveryRoute()
    {
        var expected = ExpectedState("advance_5_3_eq_35_steps");
        var factory = new RandomStreamFactory();

        // The reference expresses this as AdvanceState(e = 5, c = 3), that is 2^5 + 3 steps.
        var combined = factory.CreateStreamAt(0);
        combined.AdvanceByPowerOfTwo(5);
        combined.Advance(3);

        var direct = factory.CreateStreamAt(0);
        direct.Advance(35);

        var oneAtATime = factory.CreateStreamAt(0);
        for (var i = 0; i < 35; i++)
        {
            oneAtATime.Advance(1);
        }

        Assert.Equal(expected, combined.CurrentState.ToArray());
        Assert.Equal(expected, direct.CurrentState.ToArray());
        Assert.Equal(ExpectedState("advance_35x1_eq_advance_5_3"), oneAtATime.CurrentState.ToArray());
    }

    [Fact]
    public void TheDrawAfterAThirtyFiveStepAdvanceMatchesTheReference()
    {
        var reference = ReferenceVectors.Case("advance_35_first_u01");
        var stream = new RandomStreamFactory().CreateStream();

        stream.Advance(35);

        AssertSameBits(reference.ValueBits!, stream.NextDouble(), "advance_35_first_u01");
    }

    [Fact]
    public void ResettingToTheStreamStartMatchesTheReference()
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.SkipToNextSubstream();
        stream.NextDouble();

        stream.RewindStream();

        var expected = ExpectedState("reset_start_restores_ig");
        Assert.Equal(expected, stream.CurrentState.ToArray());
        Assert.Equal(expected, stream.SubstreamStartState.ToArray());
        Assert.Equal(expected, stream.StreamStartState.ToArray());
    }

    [Fact]
    public void NamedStreamsFromAnAllOnesSeedMatchTheReference()
    {
        var factory = new RandomStreamFactory(new Mrg32k3aState(AllOnesSeed));

        var poisson = factory.CreateStream("poisson");
        var laplace = factory.CreateStream("laplace");
        var galois = factory.CreateStream("galois");
        var cantor = factory.CreateStream("cantor");

        Assert.Equal(ExpectedState("seed1_poisson_initial"), poisson.StreamStartState.ToArray());
        Assert.Equal(ExpectedState("seed1_laplace_initial"), laplace.StreamStartState.ToArray());
        Assert.Equal(ExpectedState("seed1_galois_initial"), galois.StreamStartState.ToArray());
        Assert.Equal(ExpectedState("seed1_cantor_initial"), cantor.StreamStartState.ToArray());
    }

    [Fact]
    public void SteppingBackOneStreamLengthMatchesTheReference()
    {
        var factory = new RandomStreamFactory(new Mrg32k3aState(AllOnesSeed));
        factory.CreateStream("poisson");
        factory.CreateStream("laplace");
        var galois = factory.CreateStream("galois");

        galois.RetreatByPowerOfTwo(127);

        Assert.Equal(ExpectedState("galois_minus_2pow127_eq_laplace"), galois.CurrentState.ToArray());
    }

    [Fact]
    public void SubstreamStatesMatchTheStatesTheReferenceDrewFrom()
    {
        // The sequence cases below start from states the reference reached by moving between
        // substreams. Reaching those same states through this library's substream operations checks
        // the 2^76 jump against the reference rather than against this library's own tables.
        var factory = new RandomStreamFactory();
        factory.CreateStream();
        factory.CreateStream();
        var third = factory.CreateStream();

        Assert.Equal(ReferenceVectors.Case("substream_first5").InitialCg, third.CurrentState.ToArray());

        for (var i = 0; i < 4; i++)
        {
            third.SkipToNextSubstream();
        }

        Assert.Equal(ReferenceVectors.Case("substream_4th_next_first5").InitialCg, third.CurrentState.ToArray());

        var named = new RandomStreamFactory(new Mrg32k3aState(AllOnesSeed));
        named.CreateStream("poisson");
        named.CreateStream("laplace");
        var galois = named.CreateStream("galois");
        galois.SkipToNextSubstream();

        Assert.Equal(ReferenceVectors.Case("seed1_galois_nextsub_u01").InitialCg, galois.CurrentState.ToArray());
    }

    [Fact]
    public void TheHundredThousandDrawChecksumOfTheSecondStreamMatchesTheReference()
    {
        var reference = ReferenceVectors.Case("checksum_g2_100k_nextsub");
        var factory = new RandomStreamFactory();
        factory.CreateStream("g1");
        var second = factory.CreateStream("g2");

        second.SkipToNextSubstream();

        var total = 0.0;
        for (var i = 0; i < 100_000; i++)
        {
            total += second.NextDouble();
        }

        AssertSameBits(reference.ValueBits!, total, "checksum_g2_100k_nextsub");
    }

    [Fact]
    public void TheHundredThousandAntitheticChecksumOfTheThirdStreamMatchesTheReference()
    {
        var reference = ReferenceVectors.Case("checksum_g3_100k_anti");
        var factory = new RandomStreamFactory();
        factory.CreateStream("g1");
        factory.CreateStream("g2");
        var third = factory.CreateStream("g3");

        for (var i = 0; i < 4; i++)
        {
            third.SkipToNextSubstream();
        }

        for (var i = 0; i < 5; i++)
        {
            third.NextDouble();
        }

        third.Antithetic = true;

        var total = 0.0;
        for (var i = 0; i < 100_000; i++)
        {
            total += third.NextDouble();
        }

        AssertSameBits(reference.ValueBits!, total, "checksum_g3_100k_anti");
    }

    [Fact]
    public void TheHundredThousandHighPrecisionChecksumMatchesTheReference()
    {
        // This value used to exist only as four decimal places inside a description string, a
        // resolution at which a one-unit-in-the-last-place defect on every draw would move it by
        // about 7e-12 and pass. It now ships with a bit pattern.
        var reference = ReferenceVectors.Case("checksum_g2_100k_incprec");
        var factory = new RandomStreamFactory();
        factory.CreateStream("g1");
        var second = factory.CreateStream("g2");

        second.RewindSubstream();
        second.HighPrecision = true;

        var total = 0.0;
        for (var i = 0; i < 100_000; i++)
        {
            total += second.NextDouble();
        }

        AssertSameBits(reference.ValueBits!, total, "checksum_g2_100k_incprec");
        Assert.Equal("50098.2241", total.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void EveryCaseInTheVectorFileIsEitherReproducedOrExplained()
    {
        var covered = new HashSet<string>(StringComparer.Ordinal);
        covered.UnionWith(ReferenceVectors.NamesOfKind("u01_sequence"));
        covered.UnionWith(ReferenceVectors.NamesOfKind("randint_sequence"));
        covered.UnionWith(new[]
        {
            "initial_s0_is_default_seed", "initial_s1_known", "initial_s2_known",
            "spacing_s0_plus_2pow127_eq_s1", "advance_5_3_eq_35_steps", "advance_35x1_eq_advance_5_3",
            "advance_35_first_u01", "reset_start_restores_ig",
            "seed1_poisson_initial", "seed1_laplace_initial", "seed1_galois_initial", "seed1_cantor_initial",
            "galois_minus_2pow127_eq_laplace",
            "checksum_g2_100k_nextsub", "checksum_g3_100k_anti", "checksum_g2_100k_incprec",
            "checksum_testRngStream_final", "testRngStream_driver",
        });

        var unaccounted = ReferenceVectors.File.Cases
            .Select(c => c.Name)
            .Where(name => !covered.Contains(name) && !NotReproduced.ContainsKey(name))
            .ToArray();

        Assert.Empty(unaccounted);
        Assert.Equal(ReferenceVectors.File.Cases.Length, covered.Count + NotReproduced.Count);
    }

    [Fact]
    public void EveryReferenceCaseAgreedWithItsPublishedValue()
    {
        // The state checks carry the reference's own comparison against the published .res files.
        // If any of them were false the vectors themselves would be suspect.
        var checks = ReferenceVectors.File.Cases.Where(c => c.Match.HasValue).ToArray();

        Assert.NotEmpty(checks);
        Assert.All(checks, c => Assert.True(c.Match!.Value, $"{c.Name} did not match in the reference run"));
        Assert.All(checks, c => Assert.Equal(ExpectedState(c.Name), c.Actual));
    }

    private static bool HasOverride()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ReferenceVectors.OverrideVariable));
    }

    private static TheoryData<string> ToTheoryData(string kind)
    {
        var data = new TheoryData<string>();
        foreach (var name in ReferenceVectors.NamesOfKind(kind))
        {
            data.Add(name);
        }

        return data;
    }

    private static uint[] ExpectedState(string caseName)
    {
        return ReferenceVectors.Case(caseName).Expected!.Value.EnumerateArray()
            .Select(element => element.GetUInt32())
            .ToArray();
    }

    private static RandomStream StreamPositionedAt(uint[] current)
    {
        return RandomStream.FromState(new RandomStreamState
        {
            StreamStart = (uint[])current.Clone(),
            SubstreamStart = (uint[])current.Clone(),
            Current = (uint[])current.Clone(),
        });
    }

    private static void AssertSameBits(string expectedBits, double actual, string what)
    {
        ReferenceVectors.AssertSameBits(expectedBits, actual, what);
    }
}
