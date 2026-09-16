namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Checks the two output modifiers: the 53-bit high-precision mode and antithetic variates,
/// alone and together.
/// </summary>
public class PrecisionAndAntitheticTests
{
    [Fact]
    public void HighPrecisionProducesTheTwoStepCombination()
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.HighPrecision = true;

        Assert.Equal(0.12701114103229952, stream.NextDouble());
        Assert.Equal(0.309186064807579, stream.NextDouble());
        Assert.Equal(0.2216299475748655, stream.NextDouble());
    }

    [Fact]
    public void HighPrecisionIsTheDocumentedFormula()
    {
        var factory = new RandomStreamFactory();
        var plain = factory.CreateStreamAt(0);
        var precise = factory.CreateStreamAt(0);
        precise.HighPrecision = true;

        for (var i = 0; i < 1000; i++)
        {
            var first = plain.NextDouble();
            var second = plain.NextDouble();
            var expected = first + (second * Mrg32k3aConstants.HighPrecisionWeight);
            if (expected >= 1.0)
            {
                expected -= 1.0;
            }

            Assert.Equal(expected, precise.NextDouble());
        }
    }

    [Fact]
    public void HighPrecisionAdvancesTheStateByTwoSteps()
    {
        var factory = new RandomStreamFactory();
        var plain = factory.CreateStreamAt(0);
        var precise = factory.CreateStreamAt(0);
        precise.HighPrecision = true;

        precise.NextDouble();
        plain.NextDouble();
        plain.NextDouble();

        Assert.Equal(plain.CurrentState, precise.CurrentState);
    }

    [Fact]
    public void ExplicitHighPrecisionDrawIgnoresTheFlag()
    {
        var factory = new RandomStreamFactory();
        var viaFlag = factory.CreateStreamAt(0);
        var viaMethod = factory.CreateStreamAt(0);
        viaFlag.HighPrecision = true;

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(viaFlag.NextDouble(), viaMethod.NextDoubleHighPrecision());
        }
    }

    [Fact]
    public void TheHighPrecisionWeightScalesExactly()
    {
        // The weight is 2^-24, a power of two, so scaling a draw by it only decrements an exponent.
        // No rounding happens there, and the mantissa is nowhere near the subnormal range. The
        // high-precision value is therefore a single correctly rounded addition.
        var stream = new RandomStreamFactory().CreateStream();

        for (var i = 0; i < 200_000; i++)
        {
            var u = stream.NextDouble();
            var scaled = u * Mrg32k3aConstants.HighPrecisionWeight;

            Assert.Equal(u, scaled / Mrg32k3aConstants.HighPrecisionWeight);
            Assert.True(double.IsNormal(scaled), $"scaling {u} left the normal range");
        }
    }

    [Fact]
    public void HighPrecisionIsUnaffectedByFusedMultiplyAddContraction()
    {
        // Because the weighting is exact, computing the combination with a fused multiply-add gives
        // the identical result. A runtime or architecture that contracts the expression cannot
        // change the sequence, which is what makes the output portable.
        var factory = new RandomStreamFactory();
        var plain = factory.CreateStreamAt(0);
        var precise = factory.CreateStreamAt(0);
        precise.HighPrecision = true;

        for (var i = 0; i < 50_000; i++)
        {
            var first = plain.NextDouble();
            var second = plain.NextDouble();
            var fused = Math.FusedMultiplyAdd(second, Mrg32k3aConstants.HighPrecisionWeight, first);
            if (fused >= 1.0)
            {
                fused -= 1.0;
            }

            Assert.Equal(fused, precise.NextDouble());
        }
    }

    [Fact]
    public void AntitheticProducesOneMinusThePlainDraw()
    {
        var stream = new RandomStreamFactory().CreateStream();
        stream.Antithetic = true;

        Assert.Equal(0.8729888779534228, stream.NextDouble());
        Assert.Equal(0.6814724346032055, stream.NextDouble());
        Assert.Equal(0.6908139844167299, stream.NextDouble());
    }

    [Fact]
    public void AntitheticDoesNotChangeHowFarADrawAdvancesTheState()
    {
        var factory = new RandomStreamFactory();
        var plain = factory.CreateStreamAt(0);
        var reflected = factory.CreateStreamAt(0);
        reflected.Antithetic = true;

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(1.0 - plain.NextDouble(), reflected.NextDouble());
        }

        Assert.Equal(plain.CurrentState, reflected.CurrentState);
    }

    [Fact]
    public void TurningAntitheticOffRestoresThePlainSequence()
    {
        var factory = new RandomStreamFactory();
        var plain = factory.CreateStreamAt(0);
        var toggled = factory.CreateStreamAt(0);

        plain.NextDouble();
        toggled.Antithetic = true;
        toggled.NextDouble();
        toggled.Antithetic = false;

        Assert.Equal(plain.NextDouble(), toggled.NextDouble());
    }

    [Fact]
    public void AntitheticReflectsEachUnderlyingDrawInHighPrecisionMode()
    {
        // L'Ecuyer et al. (2002) spells the high-precision formula out only for the non-antithetic case.
        // The reference vectors settle it: both underlying draws are reflected and the weighted term
        // carries an offset of minus one. Reflecting the combined value instead agrees to within one
        // unit in the last place, which is why this needs an exact comparison.
        var factory = new RandomStreamFactory();
        var plain = factory.CreateStreamAt(0);
        var both = factory.CreateStreamAt(0);
        both.HighPrecision = true;
        both.Antithetic = true;

        for (var i = 0; i < 1000; i++)
        {
            var first = 1.0 - plain.NextDouble();
            var second = 1.0 - plain.NextDouble();
            var expected = first + ((second - 1.0) * Mrg32k3aConstants.HighPrecisionWeight);
            if (expected < 0.0)
            {
                expected += 1.0;
            }

            Assert.Equal(expected, both.NextDouble());
        }
    }

    [Fact]
    public void ReflectingTheCombinedValueIsNotTheSameAsReflectingEachDraw()
    {
        // Guards against quietly sliding back to the simpler reading: the two must actually differ,
        // otherwise the test above would prove nothing.
        var factory = new RandomStreamFactory();
        var precise = factory.CreateStreamAt(0);
        var both = factory.CreateStreamAt(0);
        precise.HighPrecision = true;
        both.HighPrecision = true;
        both.Antithetic = true;

        var differences = 0;
        for (var i = 0; i < 1000; i++)
        {
            if (1.0 - precise.NextDouble() != both.NextDouble())
            {
                differences++;
            }
        }

        Assert.True(differences > 100, $"only {differences} of 1000 draws distinguished the two readings");
    }

    [Fact]
    public void EveryModeStaysStrictlyInsideTheUnitInterval()
    {
        var factory = new RandomStreamFactory();

        foreach (var antithetic in new[] { false, true })
        {
            foreach (var highPrecision in new[] { false, true })
            {
                var stream = factory.CreateStream();
                stream.Antithetic = antithetic;
                stream.HighPrecision = highPrecision;

                for (var i = 0; i < 50_000; i++)
                {
                    var u = stream.NextDouble();
                    Assert.True(u > 0.0 && u < 1.0, $"draw {u} left the open unit interval");
                }
            }
        }
    }

    [Fact]
    public void HighPrecisionResolvesFinerThanNormalPrecision()
    {
        var factory = new RandomStreamFactory();
        var plain = factory.CreateStreamAt(0);
        var precise = factory.CreateStreamAt(0);
        precise.HighPrecision = true;

        var plainDistinct = 0;
        for (var i = 0; i < 2000; i++)
        {
            var u = precise.NextDouble();
            if (Math.Abs((u / Mrg32k3aConstants.NormalizationFactor) - Math.Round(u / Mrg32k3aConstants.NormalizationFactor)) > 1e-6)
            {
                plainDistinct++;
            }
        }

        Assert.True(
            plainDistinct > 1900,
            "high-precision draws should almost never land on the 32-bit grid");
        Assert.NotEqual(plain.NextDouble(), precise.NextDouble());
    }
}
