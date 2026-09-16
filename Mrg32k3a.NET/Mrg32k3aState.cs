using System;
using System.Globalization;
using System.Text;

namespace Mrg32k3a.NET;

/// <summary>
/// The six values that make up a position of the MRG32k3a generator, used both as a factory seed and
/// as the state of a stream.
/// </summary>
/// <remarks>
/// <para>
/// The first three values belong to the first component, in the order x1[n-3], x1[n-2], x1[n-1];
/// the last three belong to the second component in the same order. Every constructor enforces the
/// seed rules of L'Ecuyer et al. (2002): the first three values below 4294967087 and not all zero,
/// the last three below 4294944443 and not all zero.
/// </para>
/// <para>
/// The <see langword="default"/> value is all zeros, which breaks those rules. It is rejected
/// wherever a seed is accepted.
/// </para>
/// </remarks>
public readonly struct Mrg32k3aState : IEquatable<Mrg32k3aState>
{
    /// <summary>Number of values in a state.</summary>
    public const int Length = StreamStateVector.Length;

    private readonly StreamStateVector _vector;

    /// <summary>Creates a state from its six values.</summary>
    /// <param name="s10">x1[n-3], below 4294967087.</param>
    /// <param name="s11">x1[n-2], below 4294967087.</param>
    /// <param name="s12">x1[n-1], below 4294967087.</param>
    /// <param name="s20">x2[n-3], below 4294944443.</param>
    /// <param name="s21">x2[n-2], below 4294944443.</param>
    /// <param name="s22">x2[n-1], below 4294944443.</param>
    /// <exception cref="ArgumentException">The values break one of the seed rules.</exception>
    public Mrg32k3aState(uint s10, uint s11, uint s12, uint s20, uint s21, uint s22)
    {
        if (!StreamStateVector.Validate(s10, s11, s12, s20, s21, s22, out var error))
        {
            throw new ArgumentException(error);
        }

        _vector = StreamStateVector.FromValues(s10, s11, s12, s20, s21, s22);
    }

    /// <summary>Creates a state from an array of six values in state order.</summary>
    /// <param name="values">The six values; the array is copied, not held.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The values break one of the seed rules.</exception>
    public Mrg32k3aState(uint[] values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (!StreamStateVector.Validate(values, out var error))
        {
            throw new ArgumentException(error, nameof(values));
        }

        _vector = StreamStateVector.FromArray(values);
    }

    internal Mrg32k3aState(StreamStateVector vector)
    {
        _vector = vector;
    }

    /// <summary>Gets the default seed of L'Ecuyer et al. (2002), six copies of 12345.</summary>
    public static Mrg32k3aState DefaultSeed { get; } = new Mrg32k3aState(
        StreamStateVector.FromValues(
            Mrg32k3aConstants.DefaultSeedComponent,
            Mrg32k3aConstants.DefaultSeedComponent,
            Mrg32k3aConstants.DefaultSeedComponent,
            Mrg32k3aConstants.DefaultSeedComponent,
            Mrg32k3aConstants.DefaultSeedComponent,
            Mrg32k3aConstants.DefaultSeedComponent));

    /// <summary>Gets one of the six values in state order.</summary>
    /// <param name="index">A position from zero to five.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside zero to five.</exception>
    public uint this[int index] => index switch
    {
        0 => (uint)_vector.S10,
        1 => (uint)_vector.S11,
        2 => (uint)_vector.S12,
        3 => (uint)_vector.S20,
        4 => (uint)_vector.S21,
        5 => (uint)_vector.S22,
        _ => throw new ArgumentOutOfRangeException(nameof(index), index, "The index must be between 0 and 5 inclusive."),
    };

    internal StreamStateVector Vector => _vector;

    /// <summary>Compares two states for equality.</summary>
    /// <param name="left">The first state.</param>
    /// <param name="right">The second state.</param>
    /// <returns><see langword="true"/> when all six values match.</returns>
    public static bool operator ==(Mrg32k3aState left, Mrg32k3aState right) => left.Equals(right);

    /// <summary>Compares two states for inequality.</summary>
    /// <param name="left">The first state.</param>
    /// <param name="right">The second state.</param>
    /// <returns><see langword="true"/> when any of the six values differ.</returns>
    public static bool operator !=(Mrg32k3aState left, Mrg32k3aState right) => !left.Equals(right);

    /// <summary>Tries to create a state from an array of six values in state order.</summary>
    /// <param name="values">The candidate values.</param>
    /// <param name="state">Receives the state when the values are valid, otherwise <see langword="default"/>.</param>
    /// <param name="error">Receives a description of the first rule broken, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the values form a usable state.</returns>
    public static bool TryCreate(uint[]? values, out Mrg32k3aState state, out string? error)
    {
        if (!StreamStateVector.Validate(values!, out error))
        {
            state = default;
            return false;
        }

        state = new Mrg32k3aState(StreamStateVector.FromArray(values!));
        return true;
    }

    /// <summary>Returns the six values in state order.</summary>
    /// <returns>A new array of six unsigned integers.</returns>
    public uint[] ToArray()
    {
        return _vector.ToArray();
    }

    /// <inheritdoc/>
    public bool Equals(Mrg32k3aState other)
    {
        return _vector.Equals(other._vector);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is Mrg32k3aState other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return _vector.GetHashCode();
    }

    /// <summary>Returns the six values separated by commas.</summary>
    /// <returns>A single line of text.</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        AppendTo(builder);
        return builder.ToString();
    }

    internal void AppendTo(StringBuilder builder)
    {
        for (var i = 0; i < Length; i++)
        {
            if (i != 0)
            {
                builder.Append(", ");
            }

            builder.Append(this[i].ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Throws when <paramref name="state"/> is the invalid all-zero default.</summary>
    /// <param name="state">The state to check.</param>
    /// <param name="parameterName">The name of the parameter it arrived through.</param>
    internal static void ThrowIfDefault(Mrg32k3aState state, string parameterName)
    {
        if (state._vector.S10 == 0 && state._vector.S11 == 0 && state._vector.S12 == 0)
        {
            throw new ArgumentException(
                "The state is uninitialised; the default value is not a valid state.",
                parameterName);
        }
    }
}
