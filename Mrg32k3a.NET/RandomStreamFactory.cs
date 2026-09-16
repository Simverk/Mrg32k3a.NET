using System;
using static Mrg32k3a.NET.Mrg32k3aConstants;

namespace Mrg32k3a.NET;

/// <summary>
/// A source of non-overlapping <see cref="RandomStream"/> instances. The factory owns a seed and a
/// creation order: the first stream it hands out starts at that seed, and every later stream starts
/// 2^127 values beyond the one before it.
/// </summary>
/// <remarks>
/// <para>
/// Use a separate instance for each independent piece of work, such as a simulation run, an
/// experiment or a test. Factories built from the same seed produce the same sequence of streams
/// without affecting each other, so separate pieces of work stay reproducible even when they run at
/// the same time. To start again from a different seed, create a new factory.
/// </para>
/// <para>
/// Handing out a stream is thread safe. The streams themselves are not, so give each worker its own.
/// </para>
/// </remarks>
public sealed class RandomStreamFactory
{
    private readonly object _gate = new object();
    private readonly Mrg32k3aState _seed;
    private StreamStateVector _nextSeed;
    private long _createdStreamCount;

    /// <summary>Creates a factory seeded with <see cref="Mrg32k3aState.DefaultSeed"/>, six copies of 12345.</summary>
    public RandomStreamFactory()
        : this(Mrg32k3aState.DefaultSeed)
    {
    }

    /// <summary>Creates a factory with an explicit seed.</summary>
    /// <param name="seed">The initial state of the first stream.</param>
    /// <exception cref="ArgumentException"><paramref name="seed"/> is the uninitialised default value.</exception>
    public RandomStreamFactory(Mrg32k3aState seed)
    {
        Mrg32k3aState.ThrowIfDefault(seed, nameof(seed));
        _seed = seed;
        _nextSeed = seed.Vector;
    }

    /// <summary>Gets the seed of this factory, which is the initial state of its first stream.</summary>
    public Mrg32k3aState Seed => _seed;

    /// <summary>
    /// Gets the number of streams this factory has handed out through <see cref="CreateStream"/> and
    /// <see cref="CreateStreams"/>.
    /// </summary>
    public long CreatedStreamCount
    {
        get
        {
            lock (_gate)
            {
                return _createdStreamCount;
            }
        }
    }

    /// <summary>
    /// Hands out the next stream, starting 2^127 values beyond the stream handed out before it.
    /// </summary>
    /// <param name="name">An optional label, used only in diagnostics.</param>
    /// <returns>A new stream positioned at the start of its own block.</returns>
    public RandomStream CreateStream(string? name = null)
    {
        StreamStateVector seed;
        lock (_gate)
        {
            seed = TakeNextSeed();
        }

        return new RandomStream(seed, name);
    }

    /// <summary>
    /// Hands out the next <paramref name="count"/> streams in creation order, typically one for each
    /// worker.
    /// </summary>
    /// <param name="count">The number of streams to hand out.</param>
    /// <returns>
    /// A new array of streams, identical to calling <see cref="CreateStream"/> that many times. The
    /// streams are consecutive even when other threads create streams at the same time.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public RandomStream[] CreateStreams(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "The count must not be negative.");
        }

        var seeds = new StreamStateVector[count];
        lock (_gate)
        {
            for (var i = 0; i < count; i++)
            {
                seeds[i] = TakeNextSeed();
            }
        }

        var streams = new RandomStream[count];
        for (var i = 0; i < count; i++)
        {
            streams[i] = new RandomStream(seeds[i], null);
        }

        return streams;
    }

    /// <summary>
    /// Builds the stream at a given position in this factory's ordering without creating the ones
    /// before it, and without touching the creation order.
    /// </summary>
    /// <param name="index">Zero-based position; index zero is the stream that starts at the factory seed.</param>
    /// <param name="name">An optional label, used only in diagnostics.</param>
    /// <returns>A stream identical to the one <see cref="CreateStream"/> would produce at that position.</returns>
    /// <remarks>
    /// Unlike <see cref="CreateStream"/>, this identifies a stream by its position rather than by
    /// creation order. It exists for parallel and distributed runs, where a worker knows its rank but
    /// not the history of the run. The cost grows with the logarithm of <paramref name="index"/>, not
    /// with <paramref name="index"/> itself.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative.</exception>
    public RandomStream CreateStreamAt(long index, string? name = null)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The stream index must not be negative.");
        }

        var seed = _seed.Vector;
        if (index > 0)
        {
            seed.Jump(
                ModularMatrix.Power(A1P127, (ulong)index, M1),
                ModularMatrix.Power(A2P127, (ulong)index, M2));
        }

        return new RandomStream(seed, name);
    }

    /// <summary>Returns the next stream start and moves the creation order on by one. Call under the gate.</summary>
    private StreamStateVector TakeNextSeed()
    {
        var seed = _nextSeed;
        _nextSeed.Jump(A1P127, A2P127);
        _createdStreamCount++;
        return seed;
    }
}
