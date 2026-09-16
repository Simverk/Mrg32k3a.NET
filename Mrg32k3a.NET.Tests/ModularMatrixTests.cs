
namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Recomputes the jump tables from the one-step transition matrices so that the embedded constants
/// can never drift away from the recurrence they are supposed to describe.
/// </summary>
public class ModularMatrixTests
{
    public static TheoryData<string> JumpTableNames => new()
    {
        "A1P76", "A2P76", "A1P127", "A2P127",
    };

    [Theory]
    [MemberData(nameof(JumpTableNames))]
    public void EmbeddedJumpTablesEqualRepeatedSquaringOfTheTransitionMatrix(string table)
    {
        var (source, expected, exponent, modulus) = table switch
        {
            "A1P76" => (Mrg32k3aConstants.A1, Mrg32k3aConstants.A1P76, 76, Mrg32k3aConstants.M1),
            "A2P76" => (Mrg32k3aConstants.A2, Mrg32k3aConstants.A2P76, 76, Mrg32k3aConstants.M2),
            "A1P127" => (Mrg32k3aConstants.A1, Mrg32k3aConstants.A1P127, 127, Mrg32k3aConstants.M1),
            _ => (Mrg32k3aConstants.A2, Mrg32k3aConstants.A2P127, 127, Mrg32k3aConstants.M2),
        };

        var recomputed = ModularMatrix.PowerOfTwoPower(source, exponent, modulus);

        Assert.Equal(expected, recomputed);
    }

    [Fact]
    public void InverseTransitionMatricesUndoTheTransitionMatrices()
    {
        Assert.Equal(
            ModularMatrix.Identity(),
            ModularMatrix.Multiply(Mrg32k3aConstants.InvA1, Mrg32k3aConstants.A1, Mrg32k3aConstants.M1));
        Assert.Equal(
            ModularMatrix.Identity(),
            ModularMatrix.Multiply(Mrg32k3aConstants.InvA2, Mrg32k3aConstants.A2, Mrg32k3aConstants.M2));
    }

    [Fact]
    public void InverseTablesEqualTheMatricesDerivedFromTheRecurrence()
    {
        // Solving x1[n] = a12 x1[n-2] - a13 x1[n-3] for the oldest term gives the backward step.
        var inverseOfA13 = ModularMatrix.ModularInverse(Mrg32k3aConstants.A13, Mrg32k3aConstants.M1);
        var inverseOfA23 = ModularMatrix.ModularInverse(Mrg32k3aConstants.A23, Mrg32k3aConstants.M2);

        var expectedInvA1 = new[]
        {
            Mrg32k3aConstants.A12 * inverseOfA13 % Mrg32k3aConstants.M1,
            0UL,
            Mrg32k3aConstants.M1 - inverseOfA13,
            1UL, 0UL, 0UL,
            0UL, 1UL, 0UL,
        };
        var expectedInvA2 = new[]
        {
            0UL,
            Mrg32k3aConstants.A21 * inverseOfA23 % Mrg32k3aConstants.M2,
            Mrg32k3aConstants.M2 - inverseOfA23,
            1UL, 0UL, 0UL,
            0UL, 1UL, 0UL,
        };

        Assert.Equal(expectedInvA1, Mrg32k3aConstants.InvA1);
        Assert.Equal(expectedInvA2, Mrg32k3aConstants.InvA2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(16)]
    public void PowerOfTwoJumpAgreesWithSteppingTheGeneratorDirectly(int exponent)
    {
        var factory = new RandomStreamFactory();
        var stepped = factory.CreateStream();
        var jumped = factory.CreateStreamAt(0);

        var steps = 1L << exponent;
        for (var i = 0L; i < steps; i++)
        {
            stepped.NextDouble();
        }

        jumped.AdvanceByPowerOfTwo(exponent);

        Assert.Equal(stepped.CurrentState, jumped.CurrentState);
    }

    [Fact]
    public void ModularInverseRoundTripsForTheGeneratorCoefficients()
    {
        Assert.Equal(
            1UL,
            Mrg32k3aConstants.A13
            * ModularMatrix.ModularInverse(Mrg32k3aConstants.A13, Mrg32k3aConstants.M1)
            % Mrg32k3aConstants.M1);
        Assert.Equal(
            1UL,
            Mrg32k3aConstants.A23
            * ModularMatrix.ModularInverse(Mrg32k3aConstants.A23, Mrg32k3aConstants.M2)
            % Mrg32k3aConstants.M2);
    }
}
