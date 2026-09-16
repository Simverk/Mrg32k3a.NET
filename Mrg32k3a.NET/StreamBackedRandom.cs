using System;

namespace Mrg32k3a.NET;

/// <summary>
/// Presents a <see cref="RandomStream"/> as a <see cref="Random"/>, so a stream can be handed to any
/// API or library that asks for the framework type.
/// </summary>
/// <remarks>
/// <para>
/// The adapter holds the stream rather than copying it, so draws taken through the adapter and draws
/// taken directly from the stream come from the same sequence and advance the same state.
/// </para>
/// <para>
/// Like the stream it wraps, an adapter is not thread safe.
/// </para>
/// </remarks>
public sealed class StreamBackedRandom : Random
{
    private readonly RandomStream _stream;

    /// <summary>Wraps an existing stream.</summary>
    /// <param name="stream">The stream to present as a <see cref="Random"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public StreamBackedRandom(RandomStream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    /// <summary>Gets the stream behind this adapter.</summary>
    public RandomStream Stream => _stream;

    /// <inheritdoc/>
    public override int Next()
    {
        return _stream.Next(int.MaxValue);
    }

    /// <inheritdoc/>
    public override int Next(int maxValue)
    {
        return _stream.Next(maxValue);
    }

    /// <inheritdoc/>
    public override int Next(int minValue, int maxValue)
    {
        return _stream.Next(minValue, maxValue);
    }

    /// <inheritdoc/>
    public override double NextDouble()
    {
        return _stream.NextDouble();
    }

    /// <inheritdoc/>
    public override void NextBytes(byte[] buffer)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)_stream.Next(0, 256);
        }
    }

#if NET8_0_OR_GREATER
    /// <inheritdoc/>
    public override void NextBytes(Span<byte> buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)_stream.Next(0, 256);
        }
    }

    /// <inheritdoc/>
    public override long NextInt64()
    {
        return _stream.NextInt64(long.MaxValue);
    }

    /// <inheritdoc/>
    public override long NextInt64(long maxValue)
    {
        return _stream.NextInt64(maxValue);
    }

    /// <inheritdoc/>
    public override long NextInt64(long minValue, long maxValue)
    {
        return _stream.NextInt64(minValue, maxValue);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Draws close enough to one round up to <c>1f</c> when narrowed, so those are pulled back to the
    /// largest single below one to keep the half-open range.
    /// </remarks>
    public override float NextSingle()
    {
        var value = (float)_stream.NextDouble();
        return value < 1f ? value : MathF.BitDecrement(1f);
    }
#endif

    /// <inheritdoc/>
    protected override double Sample()
    {
        return _stream.NextDouble();
    }
}
