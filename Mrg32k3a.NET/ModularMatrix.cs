using System;

namespace Mrg32k3a.NET;

/// <summary>
/// Arithmetic on three by three matrices modulo a prime below 2^32, used to jump a component of
/// the generator forward or backward by an arbitrary number of steps.
/// </summary>
/// <remarks>
/// Every entry stays below 2^32, so a product of two entries fits exactly in a 64 bit unsigned
/// integer. Each product is reduced before the three terms of a dot product are summed, which keeps
/// the running total below 2^34 and makes the whole class overflow free.
/// </remarks>
internal static class ModularMatrix
{
    /// <summary>Number of rows and columns of every matrix handled here.</summary>
    internal const int Order = 3;

    /// <summary>Number of entries in a row-major matrix.</summary>
    internal const int Size = Order * Order;

    /// <summary>Returns the three by three identity matrix.</summary>
    internal static ulong[] Identity()
    {
        return new ulong[Size] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
    }

    /// <summary>Multiplies two matrices modulo <paramref name="m"/>.</summary>
    /// <param name="a">Left operand, row major, nine entries.</param>
    /// <param name="b">Right operand, row major, nine entries.</param>
    /// <param name="m">The modulus, which must be below 2^32.</param>
    /// <returns>A new matrix holding the reduced product.</returns>
    internal static ulong[] Multiply(ulong[] a, ulong[] b, ulong m)
    {
        var result = new ulong[Size];
        for (var row = 0; row < Order; row++)
        {
            for (var column = 0; column < Order; column++)
            {
                ulong sum = 0;
                for (var k = 0; k < Order; k++)
                {
                    sum += a[(row * Order) + k] * b[(k * Order) + column] % m;
                }

                result[(row * Order) + column] = sum % m;
            }
        }

        return result;
    }

    /// <summary>Applies a matrix to a three element vector modulo <paramref name="m"/>.</summary>
    /// <param name="a">The matrix, row major, nine entries.</param>
    /// <param name="v0">First vector component.</param>
    /// <param name="v1">Second vector component.</param>
    /// <param name="v2">Third vector component.</param>
    /// <param name="r0">Receives the first component of the image.</param>
    /// <param name="r1">Receives the second component of the image.</param>
    /// <param name="r2">Receives the third component of the image.</param>
    /// <param name="m">The modulus, which must be below 2^32.</param>
    internal static void Apply(
        ulong[] a,
        ulong v0,
        ulong v1,
        ulong v2,
        out ulong r0,
        out ulong r1,
        out ulong r2,
        ulong m)
    {
        r0 = ((a[0] * v0 % m) + (a[1] * v1 % m) + (a[2] * v2 % m)) % m;
        r1 = ((a[3] * v0 % m) + (a[4] * v1 % m) + (a[5] * v2 % m)) % m;
        r2 = ((a[6] * v0 % m) + (a[7] * v1 % m) + (a[8] * v2 % m)) % m;
    }

    /// <summary>Raises a matrix to the power 2^<paramref name="exponent"/> by repeated squaring.</summary>
    /// <param name="a">The base matrix, row major, nine entries.</param>
    /// <param name="exponent">The non-negative base-two exponent.</param>
    /// <param name="m">The modulus, which must be below 2^32.</param>
    /// <returns>A new matrix holding the reduced power.</returns>
    internal static ulong[] PowerOfTwoPower(ulong[] a, int exponent, ulong m)
    {
        var result = (ulong[])a.Clone();
        for (var i = 0; i < exponent; i++)
        {
            result = Multiply(result, result, m);
        }

        return result;
    }

    /// <summary>Raises a matrix to an arbitrary power by binary exponentiation.</summary>
    /// <param name="a">The base matrix, row major, nine entries.</param>
    /// <param name="exponent">The exponent.</param>
    /// <param name="m">The modulus, which must be below 2^32.</param>
    /// <returns>A new matrix holding the reduced power.</returns>
    internal static ulong[] Power(ulong[] a, ulong exponent, ulong m)
    {
        var result = Identity();
        var factor = (ulong[])a.Clone();
        while (exponent != 0)
        {
            if ((exponent & 1UL) != 0)
            {
                result = Multiply(result, factor, m);
            }

            exponent >>= 1;
            if (exponent != 0)
            {
                factor = Multiply(factor, factor, m);
            }
        }

        return result;
    }

    /// <summary>Computes the multiplicative inverse of <paramref name="a"/> modulo <paramref name="m"/>.</summary>
    /// <param name="a">The value to invert, which must be non-zero modulo <paramref name="m"/>.</param>
    /// <param name="m">The modulus, which must be coprime to <paramref name="a"/>.</param>
    /// <returns>The unique value below <paramref name="m"/> whose product with <paramref name="a"/> is one.</returns>
    /// <exception cref="ArgumentException">The values are not coprime, so no inverse exists.</exception>
    internal static ulong ModularInverse(ulong a, ulong m)
    {
        long oldRemainder = (long)(a % m);
        long remainder = (long)m;
        long oldCoefficient = 1;
        long coefficient = 0;

        while (remainder != 0)
        {
            var quotient = oldRemainder / remainder;
            (oldRemainder, remainder) = (remainder, oldRemainder - (quotient * remainder));
            (oldCoefficient, coefficient) = (coefficient, oldCoefficient - (quotient * coefficient));
        }

        if (oldRemainder != 1)
        {
            throw new ArgumentException("The value has no inverse modulo the given modulus.", nameof(a));
        }

        var inverse = oldCoefficient % (long)m;
        if (inverse < 0)
        {
            inverse += (long)m;
        }

        return (ulong)inverse;
    }
}
