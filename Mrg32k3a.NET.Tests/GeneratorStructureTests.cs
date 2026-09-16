using System.Numerics;

namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Proves structural properties of the generator rather than reproducing values: that each component
/// really does have the maximal period its parameters were chosen for, and that the stream and
/// substream jumps tile the period the way L'Ecuyer et al. (2002), Sections 1.2 and 1.3, lay out.
/// </summary>
/// <remarks>
/// These checks are independent of any published output. A typo in a modulus or a multiplier would
/// still produce a self-consistent generator with a reproducible sequence, but it would almost
/// certainly destroy the maximal period, which is what the parameter search in L'Ecuyer (1999) was
/// for.
/// </remarks>
public class GeneratorStructureTests
{
    private static readonly int[] MillerRabinBases = { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ComponentAchievesTheMaximalPeriodForItsOrderAndModulus(int component)
    {
        var (transition, modulus) = component == 1
            ? (Mrg32k3aConstants.A1, Mrg32k3aConstants.M1)
            : (Mrg32k3aConstants.A2, Mrg32k3aConstants.M2);

        var m = new BigInteger(modulus);
        var period = BigInteger.Pow(m, 3) - BigInteger.One;
        var primeFactors = new[]
        {
            new BigInteger(2),
            (m - BigInteger.One) / 2,
            (m * m) + m + BigInteger.One,
        };

        // The three factors account for the whole period, and each of them is prime, so the order of
        // the transition matrix is maximal exactly when it survives every proper divisor below.
        Assert.Equal(period, primeFactors[0] * primeFactors[1] * primeFactors[2]);
        Assert.All(primeFactors, factor => Assert.True(IsPrime(factor), $"{factor} is not prime"));

        Assert.Equal(ModularMatrix.Identity(), Power(transition, period, modulus));

        foreach (var factor in primeFactors)
        {
            Assert.NotEqual(ModularMatrix.Identity(), Power(transition, period / factor, modulus));
        }
    }

    [Fact]
    public void SubstreamJumpRaisedToTheSubstreamCountGivesTheStreamJump()
    {
        // A stream holds 2^51 substreams of 2^76 values, so applying the substream jump 2^51 times
        // must be the stream jump. This pins the three exponents against each other.
        const int substreamsPerStream = 51;

        Assert.Equal(
            Mrg32k3aConstants.StreamExponent,
            Mrg32k3aConstants.SubstreamExponent + substreamsPerStream);
        Assert.Equal(
            Mrg32k3aConstants.A1P127,
            ModularMatrix.PowerOfTwoPower(Mrg32k3aConstants.A1P76, substreamsPerStream, Mrg32k3aConstants.M1));
        Assert.Equal(
            Mrg32k3aConstants.A2P127,
            ModularMatrix.PowerOfTwoPower(Mrg32k3aConstants.A2P76, substreamsPerStream, Mrg32k3aConstants.M2));
    }

    [Fact]
    public void EachStreamStartsOneStreamLengthBeyondThePrevious()
    {
        var factory = new RandomStreamFactory();
        var first = factory.CreateStream();
        var second = factory.CreateStream();

        first.AdvanceByPowerOfTwo(Mrg32k3aConstants.StreamExponent);

        Assert.Equal(second.StreamStartState, first.CurrentState);
    }

    [Fact]
    public void TheStreamJumpIsNotReachableFromInsideASingleSubstream()
    {
        // A sanity check on the size of the blocks: a substream jump must not land where the stream
        // jump lands, which would mean the two exponents had collapsed onto each other.
        var factory = new RandomStreamFactory();
        var bySubstream = factory.CreateStreamAt(0);
        var byStream = factory.CreateStreamAt(0);

        bySubstream.AdvanceByPowerOfTwo(Mrg32k3aConstants.SubstreamExponent);
        byStream.AdvanceByPowerOfTwo(Mrg32k3aConstants.StreamExponent);

        Assert.NotEqual(bySubstream.CurrentState, byStream.CurrentState);
    }

    private static ulong[] Power(ulong[] matrix, BigInteger exponent, ulong modulus)
    {
        var result = ModularMatrix.Identity();
        var factor = (ulong[])matrix.Clone();

        while (exponent > BigInteger.Zero)
        {
            if (!exponent.IsEven)
            {
                result = ModularMatrix.Multiply(result, factor, modulus);
            }

            exponent >>= 1;
            if (exponent > BigInteger.Zero)
            {
                factor = ModularMatrix.Multiply(factor, factor, modulus);
            }
        }

        return result;
    }

    private static bool IsPrime(BigInteger n)
    {
        if (n < 2)
        {
            return false;
        }

        foreach (var small in MillerRabinBases)
        {
            if (n % small == 0)
            {
                return n == small;
            }
        }

        var d = n - BigInteger.One;
        var powersOfTwo = 0;
        while (d.IsEven)
        {
            d >>= 1;
            powersOfTwo++;
        }

        // Deterministic for every n below 3.3e24; the largest value tested here is about 1.84e19.
        foreach (var witness in MillerRabinBases)
        {
            var x = BigInteger.ModPow(witness, d, n);
            if (x == BigInteger.One || x == n - BigInteger.One)
            {
                continue;
            }

            var composite = true;
            for (var i = 0; i < powersOfTwo - 1; i++)
            {
                x = BigInteger.ModPow(x, 2, n);
                if (x == n - BigInteger.One)
                {
                    composite = false;
                    break;
                }
            }

            if (composite)
            {
                return false;
            }
        }

        return true;
    }
}
