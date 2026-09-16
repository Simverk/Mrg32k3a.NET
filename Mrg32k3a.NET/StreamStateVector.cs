using System;

namespace Mrg32k3a.NET;

/// <summary>
/// The six-value state of the backbone generator, held as fields rather than an array so that the
/// generating loop performs no bounds checks and so that copying a state is a single assignment.
/// </summary>
/// <remarks>
/// The first three values are the first component in the order x1[n-3], x1[n-2], x1[n-1]; the last
/// three are the second component in the same order. Every value is strictly below 2^32.
/// </remarks>
internal struct StreamStateVector : IEquatable<StreamStateVector>
{
    /// <summary>Number of values in a state.</summary>
    internal const int Length = 6;

    /// <summary>x1[n-3], the oldest retained value of the first component.</summary>
    internal ulong S10;

    /// <summary>x1[n-2] of the first component.</summary>
    internal ulong S11;

    /// <summary>x1[n-1], the newest value of the first component.</summary>
    internal ulong S12;

    /// <summary>x2[n-3], the oldest retained value of the second component.</summary>
    internal ulong S20;

    /// <summary>x2[n-2] of the second component.</summary>
    internal ulong S21;

    /// <summary>x2[n-1], the newest value of the second component.</summary>
    internal ulong S22;

    /// <summary>Builds a state from six unsigned integers, which must already have been validated.</summary>
    /// <param name="values">The six values in state order.</param>
    /// <returns>The corresponding state.</returns>
    internal static StreamStateVector FromArray(uint[] values)
    {
        return FromValues(values[0], values[1], values[2], values[3], values[4], values[5]);
    }

    /// <summary>Returns the state as six unsigned integers in state order.</summary>
    /// <returns>A new array of six unsigned integers.</returns>
    internal readonly uint[] ToArray()
    {
        return new[] { (uint)S10, (uint)S11, (uint)S12, (uint)S20, (uint)S21, (uint)S22 };
    }

    /// <summary>
    /// Validates a candidate seed against the seed rules of SetPackageSeed in L'Ecuyer et al. (2002).
    /// </summary>
    /// <param name="values">The candidate six values.</param>
    /// <param name="error">Receives a description of the first rule broken, or null.</param>
    /// <returns><see langword="true"/> when the values form a usable state.</returns>
    internal static bool Validate(uint[] values, out string? error)
    {
        if (values is null)
        {
            error = "The state must not be null.";
            return false;
        }

        if (values.Length != Length)
        {
            error = "The state must contain exactly six values.";
            return false;
        }

        return Validate(values[0], values[1], values[2], values[3], values[4], values[5], out error);
    }

    /// <summary>
    /// Validates six candidate values against the seed rules of SetPackageSeed in L'Ecuyer et al. (2002).
    /// </summary>
    /// <param name="s10">x1[n-3].</param>
    /// <param name="s11">x1[n-2].</param>
    /// <param name="s12">x1[n-1].</param>
    /// <param name="s20">x2[n-3].</param>
    /// <param name="s21">x2[n-2].</param>
    /// <param name="s22">x2[n-1].</param>
    /// <param name="error">Receives a description of the first rule broken, or null.</param>
    /// <returns><see langword="true"/> when the values form a usable state.</returns>
    internal static bool Validate(uint s10, uint s11, uint s12, uint s20, uint s21, uint s22, out string? error)
    {
        if (s10 >= Mrg32k3aConstants.M1 || s11 >= Mrg32k3aConstants.M1 || s12 >= Mrg32k3aConstants.M1)
        {
            error = "The first three values must all be less than 4294967087.";
            return false;
        }

        if (s10 == 0 && s11 == 0 && s12 == 0)
        {
            error = "The first three values must not all be zero.";
            return false;
        }

        if (s20 >= Mrg32k3aConstants.M2 || s21 >= Mrg32k3aConstants.M2 || s22 >= Mrg32k3aConstants.M2)
        {
            error = "The last three values must all be less than 4294944443.";
            return false;
        }

        if (s20 == 0 && s21 == 0 && s22 == 0)
        {
            error = "The last three values must not all be zero.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>Builds a state from six values, which must already have been validated.</summary>
    /// <returns>The corresponding state.</returns>
    internal static StreamStateVector FromValues(uint s10, uint s11, uint s12, uint s20, uint s21, uint s22)
    {
        StreamStateVector state;
        state.S10 = s10;
        state.S11 = s11;
        state.S12 = s12;
        state.S20 = s20;
        state.S21 = s21;
        state.S22 = s22;
        return state;
    }

    /// <summary>
    /// Applies a pair of component jump matrices, moving the state by the number of steps those
    /// matrices encode.
    /// </summary>
    /// <param name="jump1">Jump matrix for the first component, modulo m1.</param>
    /// <param name="jump2">Jump matrix for the second component, modulo m2.</param>
    internal void Jump(ulong[] jump1, ulong[] jump2)
    {
        ModularMatrix.Apply(jump1, S10, S11, S12, out var n10, out var n11, out var n12, Mrg32k3aConstants.M1);
        ModularMatrix.Apply(jump2, S20, S21, S22, out var n20, out var n21, out var n22, Mrg32k3aConstants.M2);
        S10 = n10;
        S11 = n11;
        S12 = n12;
        S20 = n20;
        S21 = n21;
        S22 = n22;
    }

    /// <inheritdoc/>
    public readonly bool Equals(StreamStateVector other)
    {
        return S10 == other.S10
            && S11 == other.S11
            && S12 == other.S12
            && S20 == other.S20
            && S21 == other.S21
            && S22 == other.S22;
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj)
    {
        return obj is StreamStateVector other && Equals(other);
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        var hash = 17L;
        hash = (hash * 31) + (long)S10;
        hash = (hash * 31) + (long)S11;
        hash = (hash * 31) + (long)S12;
        hash = (hash * 31) + (long)S20;
        hash = (hash * 31) + (long)S21;
        hash = (hash * 31) + (long)S22;
        return hash.GetHashCode();
    }
}
