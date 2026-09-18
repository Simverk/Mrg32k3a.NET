using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using static Mrg32k3a.NET.Mrg32k3aConstants;

namespace Mrg32k3a.NET;

/// <summary>
/// One stream of the MRG32k3a generator: a virtual random number generator occupying its own block of
/// 2^127 values, itself divided into 2^51 substreams of 2^76 values.
/// </summary>
/// <remarks>
/// <para>
/// Streams are handed out by a <see cref="RandomStreamFactory"/>, or rebuilt from a snapshot with
/// <see cref="FromState"/>. Each new stream starts 2^127 values beyond
/// the previous one, so streams taken from the same factory never overlap in any practical run.
/// </para>
/// <para>
/// A stream is <em>not</em> thread safe. The intended pattern is one stream per worker.
/// </para>
/// <para>
/// Internally the recurrence runs on 64 bit integers with no conditional branches, rather than on the
/// exact double-precision arithmetic of L'Ecuyer (1999), Figure 1. The observable sequence is identical and
/// does not depend on the floating point behaviour of the host, so results match across x64 and
/// ARM64 and across every target framework of this library.
/// </para>
/// </remarks>
public sealed class RandomStream
{
    private StreamStateVector _streamStart;
    private StreamStateVector _substreamStart;
    private StreamStateVector _current;
    private long _substreamIndex;
    private string _name;
    private bool _antithetic;
    private bool _highPrecision;

    internal RandomStream(StreamStateVector seed, string? name)
    {
        _streamStart = seed;
        _substreamStart = seed;
        _current = seed;
        _substreamIndex = 0;
        _name = name ?? string.Empty;
    }

    /// <summary>Gets or sets the label carried by this stream.</summary>
    /// <remarks>Setting <see langword="null"/> stores an empty label, as everywhere else a name is accepted.</remarks>
    [AllowNull]
    public string Name
    {
        get => _name;
        set => _name = value ?? string.Empty;
    }

    /// <summary>
    /// Gets or sets whether this stream returns antithetic variates, that is one minus the value it
    /// would otherwise return.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The flag does not change how far a draw advances the state, so a stream and its antithetic
    /// twin stay synchronised. Turning the flag off restores the plain sequence.
    /// </para>
    /// <para>
    /// In high-precision mode the reflection is applied to each of the two underlying draws
    /// rather than to the value they combine into. The two differ in the last bits.
    /// </para>
    /// </remarks>
    public bool Antithetic
    {
        get => _antithetic;
        set => _antithetic = value;
    }

    /// <summary>
    /// Gets or sets whether <see cref="NextDouble"/> returns roughly 53 bits of precision instead of
    /// 32, at the cost of advancing the state by two steps instead of one.
    /// </summary>
    /// <remarks>
    /// The high-precision value is the first draw plus the second draw weighted by 2^-24,
    /// reduced modulo one. When <see cref="Antithetic"/> is also set, both draws are reflected first
    /// and the weighted term carries an offset of minus one. Switching the flag changes how fast a
    /// stream is consumed, so it should be set before a stream is used rather than part way through.
    /// </remarks>
    public bool HighPrecision
    {
        get => _highPrecision;
        set => _highPrecision = value;
    }

    /// <summary>Rebuilds a stream from a snapshot taken by <see cref="SaveState"/>.</summary>
    /// <param name="state">The snapshot to restore.</param>
    /// <param name="name">An optional label overriding the one in the snapshot.</param>
    /// <returns>A stream positioned exactly where the snapshot was taken.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The snapshot is of an unknown version or holds an invalid state.</exception>
    public static RandomStream FromState(RandomStreamState state, string? name = null)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var stream = new RandomStream(default(StreamStateVector), name ?? state.Name);
        stream.LoadState(state);
        if (name is not null)
        {
            stream._name = name;
        }

        return stream;
    }

    /// <summary>Gets the current state of this stream.</summary>
    public Mrg32k3aState CurrentState => new Mrg32k3aState(_current);

    /// <summary>Gets the initial state of this stream.</summary>
    public Mrg32k3aState StreamStartState => new Mrg32k3aState(_streamStart);

    /// <summary>Gets the state at the start of the substream this stream is currently inside.</summary>
    public Mrg32k3aState SubstreamStartState => new Mrg32k3aState(_substreamStart);

    /// <summary>Gets the zero-based index of the substream this stream is currently inside.</summary>
    /// <remarks>
    /// A stream holds 2^51 substreams, so the index runs from zero to 2^51 - 1. It is part of the
    /// stream's state: it is carried by <see cref="Clone"/> and by <see cref="SaveState"/>, and it
    /// is what lets <see cref="SkipSubstreams"/> tell whether a move would leave this stream's
    /// block. Only the substream operations change it; drawing values and
    /// <see cref="Advance"/> do not.
    /// </remarks>
    public long SubstreamIndex => _substreamIndex;

    /// <summary>Returns this stream to its initial state, at the start of its first substream.</summary>
    public void RewindStream()
    {
        _substreamStart = _streamStart;
        _current = _streamStart;
        _substreamIndex = 0;
    }

    /// <summary>Returns this stream to the start of the substream it is currently inside.</summary>
    public void RewindSubstream()
    {
        _current = _substreamStart;
    }

    /// <summary>Moves this stream to the start of its next substream, 2^76 values further on.</summary>
    /// <exception cref="InvalidOperationException">
    /// This stream is already on the last of its substreams, so a further one would lie outside its
    /// own block.
    /// </exception>
    /// <remarks>
    /// The exception is a guard on the partitioning rather than a case to program around: reaching
    /// it takes 2^51 calls. Use <see cref="SkipSubstreams"/> to move by more than one substream at
    /// a time.
    /// </remarks>
    public void SkipToNextSubstream()
    {
        if (_substreamIndex >= SubstreamsPerStream - 1)
        {
            throw new InvalidOperationException(
                "This stream is already on the last of its 2^51 substreams, so it cannot move to a "
                + "next one without leaving its own block.");
        }

        _substreamStart.Jump(A1P76, A2P76);
        _current = _substreamStart;
        _substreamIndex++;
    }

    /// <summary>
    /// Moves this stream by a signed number of substreams, to the start of the substream it lands on.
    /// </summary>
    /// <param name="count">Substreams to move; negative values move back towards the stream start.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The move would leave this stream's own block, that is it would land before substream zero or
    /// at or beyond substream 2^51.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The cost grows with the logarithm of <paramref name="count"/> rather than with
    /// <paramref name="count"/> itself, because the move is one modular matrix exponentiation per
    /// component and not a run of substream jumps. Skipping a million substreams costs about as
    /// much as skipping twenty.
    /// </para>
    /// <para>
    /// A count of zero does nothing at all, and in particular does not return to the start of the
    /// current substream; <see cref="RewindSubstream"/> does that.
    /// </para>
    /// </remarks>
    public void SkipSubstreams(long count)
    {
        // Compared against the remaining headroom rather than by adding, so the check itself
        // cannot overflow for counts near the ends of the range.
        var lowest = -_substreamIndex;
        var highest = SubstreamsPerStream - 1 - _substreamIndex;
        if (count < lowest || count > highest)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                FormattableString.Invariant(
                    $"The count must be between {lowest} and {highest} inclusive at substream {_substreamIndex}."));
        }

        if (count == 0)
        {
            return;
        }

        if (count == 1)
        {
            SkipToNextSubstream();
            return;
        }

        ulong magnitude;
        ulong[] jump1;
        ulong[] jump2;
        if (count > 0)
        {
            magnitude = (ulong)count;
            jump1 = A1P76;
            jump2 = A2P76;
        }
        else
        {
            magnitude = (ulong)(-count);
            jump1 = InvA1P76;
            jump2 = InvA2P76;
        }

        _substreamStart.Jump(
            ModularMatrix.Power(jump1, magnitude, M1),
            ModularMatrix.Power(jump2, magnitude, M2));
        _current = _substreamStart;
        _substreamIndex += count;
    }

    /// <summary>Moves this stream to the start of the substream at the given index of this stream.</summary>
    /// <param name="index">A zero-based substream index below 2^51.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative, or at or beyond the 2^51 substreams this stream holds.
    /// </exception>
    /// <remarks>
    /// The index is counted from the start of this stream, not from where it happens to be, so the
    /// call lands in the same place however the stream was used beforehand. It is the operation to
    /// use to resume a run at a known replication, or to give worker <c>i</c> substream <c>i</c>
    /// without walking there. Its cost grows with the logarithm of <paramref name="index"/>.
    /// </remarks>
    public void SkipToSubstream(long index)
    {
        if (index < 0 || index >= SubstreamsPerStream)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                FormattableString.Invariant(
                    $"The index must be between 0 and {SubstreamsPerStream - 1} inclusive, the substreams a stream holds."));
        }

        RewindStream();
        SkipSubstreams(index);
    }

    /// <summary>
    /// Moves the current position of this stream by an arbitrary signed number of steps, leaving its
    /// initial state and its substream start untouched.
    /// </summary>
    /// <param name="steps">Steps to move; negative values move backwards.</param>
    /// <remarks>
    /// <para>
    /// To move 2^e + c steps, call <see cref="AdvanceByPowerOfTwo"/> (or <see cref="RetreatByPowerOfTwo"/>)
    /// with exponent <c>e</c> and then <see cref="Advance"/> with <c>c</c>. Both are escape hatches. Ordinary use is served by the
    /// reset methods and by taking more streams from the factory.
    /// </para>
    /// <para>
    /// Unlike the substream operations, this one does not refuse to leave the stream's own block.
    /// A position inside a substream can be 2^76 steps from its start, which no <see cref="long"/>
    /// can express, so there is no offset to check a move against. Enough steps here will walk into
    /// a neighbouring stream, which is the price of the escape hatch.
    /// </para>
    /// </remarks>
    public void Advance(long steps)
    {
        if (steps == 0)
        {
            return;
        }

        ulong magnitude;
        ulong[] step1;
        ulong[] step2;
        if (steps > 0)
        {
            magnitude = (ulong)steps;
            step1 = A1;
            step2 = A2;
        }
        else
        {
            magnitude = unchecked((ulong)(-steps));
            step1 = InvA1;
            step2 = InvA2;
        }

        _current.Jump(
            ModularMatrix.Power(step1, magnitude, M1),
            ModularMatrix.Power(step2, magnitude, M2));
    }

    /// <summary>
    /// Moves the current position of this stream forwards by 2^<paramref name="exponent"/> steps,
    /// leaving its initial state and its substream start untouched.
    /// </summary>
    /// <param name="exponent">A base-two exponent between 0 and 255 inclusive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exponent"/> is outside the supported range.</exception>
    public void AdvanceByPowerOfTwo(int exponent)
    {
        JumpByPowerOfTwo(exponent, A1, A2);
    }

    /// <summary>
    /// Moves the current position of this stream backwards by 2^<paramref name="exponent"/> steps,
    /// leaving its initial state and its substream start untouched.
    /// </summary>
    /// <param name="exponent">A base-two exponent between 0 and 255 inclusive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exponent"/> is outside the supported range.</exception>
    public void RetreatByPowerOfTwo(int exponent)
    {
        JumpByPowerOfTwo(exponent, InvA1, InvA2);
    }

    /// <summary>Draws the next value, uniform on the open interval from zero to one.</summary>
    /// <returns>A value strictly between zero and one.</returns>
    /// <remarks>
    /// Honours both <see cref="Antithetic"/> and <see cref="HighPrecision"/>. With high
    /// precision off the result is always an exact multiple of 1 / (2^32 - 208).
    /// </remarks>
    public double NextDouble()
    {
        if (_highPrecision)
        {
            return NextHighPrecisionSample();
        }

        var u = NextSample();
        return _antithetic ? 1.0 - u : u;
    }

    /// <summary>
    /// Draws the next value with roughly 53 bits of precision, advancing the state by two steps,
    /// whatever <see cref="HighPrecision"/> is set to.
    /// </summary>
    /// <returns>A value strictly between zero and one.</returns>
    public double NextDoubleHighPrecision()
    {
        return NextHighPrecisionSample();
    }

    /// <summary>Draws an integer uniform over a closed range.</summary>
    /// <param name="min">Smallest value that can be returned.</param>
    /// <param name="max">Largest value that can be returned, included in the range.</param>
    /// <returns>A value between <paramref name="min"/> and <paramref name="max"/> inclusive.</returns>
    /// <remarks>
    /// This makes one call to <see cref="NextDouble"/> and scales the result over the closed range. A
    /// single draw carries about 2^32 distinct values, or about 2^53 in high-precision mode, so over a
    /// range wider than that most values in the range can never be returned.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="max"/> is below <paramref name="min"/>, or the range holds more than
    /// <see cref="long.MaxValue"/> values.
    /// </exception>
    public long NextInt64Inclusive(long min, long max)
    {
        if (max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, "The upper bound must not be below the lower bound.");
        }

        var span = unchecked((ulong)max - (ulong)min);
        if (span >= long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(max),
                max,
                "The closed range must hold at most long.MaxValue values.");
        }

        var count = (long)span + 1;
        return min + ScaleToCount(NextDouble(), count);
    }

    /// <summary>Draws an integer uniform over a closed range.</summary>
    /// <param name="min">Smallest value that can be returned.</param>
    /// <param name="max">Largest value that can be returned, included in the range.</param>
    /// <returns>A value between <paramref name="min"/> and <paramref name="max"/> inclusive.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is below <paramref name="min"/>.</exception>
    public int NextInt32Inclusive(int min, int max)
    {
        return DrawInt32(min, Int32Count(min, max));
    }

    /// <summary>Draws an integer uniform over a half-open range starting at zero, following the .NET convention.</summary>
    /// <param name="maxExclusive">One past the largest value that can be returned.</param>
    /// <returns>
    /// A value at least zero and below <paramref name="maxExclusive"/>, or zero when
    /// <paramref name="maxExclusive"/> is zero.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is negative.</exception>
    public int Next(int maxExclusive)
    {
        if (maxExclusive < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                maxExclusive,
                "The exclusive upper bound must not be negative.");
        }

        return Next(0, maxExclusive);
    }

    /// <summary>Draws an integer uniform over a half-open range, following the .NET convention.</summary>
    /// <param name="minInclusive">Smallest value that can be returned.</param>
    /// <param name="maxExclusive">One past the largest value that can be returned.</param>
    /// <returns>
    /// A value at least <paramref name="minInclusive"/> and below <paramref name="maxExclusive"/>, or
    /// <paramref name="minInclusive"/> when the two bounds are equal.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is below <paramref name="minInclusive"/>.</exception>
    public int Next(int minInclusive, int maxExclusive)
    {
        if (maxExclusive < minInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                maxExclusive,
                "The exclusive upper bound must not be below the lower bound.");
        }

        return maxExclusive == minInclusive
            ? minInclusive
            : NextInt32Inclusive(minInclusive, maxExclusive - 1);
    }

    /// <summary>Draws an integer uniform over a half-open range starting at zero, following the .NET convention.</summary>
    /// <param name="maxExclusive">One past the largest value that can be returned.</param>
    /// <returns>
    /// A value at least zero and below <paramref name="maxExclusive"/>, or zero when
    /// <paramref name="maxExclusive"/> is zero.
    /// </returns>
    /// <remarks>See <see cref="NextInt64Inclusive"/> for how many distinct values a wide range can yield.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is negative.</exception>
    public long NextInt64(long maxExclusive)
    {
        if (maxExclusive < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                maxExclusive,
                "The exclusive upper bound must not be negative.");
        }

        return NextInt64(0, maxExclusive);
    }

    /// <summary>Draws an integer uniform over a half-open range, following the .NET convention.</summary>
    /// <param name="minInclusive">Smallest value that can be returned.</param>
    /// <param name="maxExclusive">One past the largest value that can be returned.</param>
    /// <returns>
    /// A value at least <paramref name="minInclusive"/> and below <paramref name="maxExclusive"/>, or
    /// <paramref name="minInclusive"/> when the two bounds are equal.
    /// </returns>
    /// <remarks>See <see cref="NextInt64Inclusive"/> for how many distinct values a wide range can yield.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxExclusive"/> is below <paramref name="minInclusive"/>, or the range holds more
    /// than <see cref="long.MaxValue"/> values.
    /// </exception>
    public long NextInt64(long minInclusive, long maxExclusive)
    {
        if (maxExclusive < minInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive),
                maxExclusive,
                "The exclusive upper bound must not be below the lower bound.");
        }

        return maxExclusive == minInclusive
            ? minInclusive
            : NextInt64Inclusive(minInclusive, maxExclusive - 1);
    }

    /// <summary>Fills part of an array with values uniform on the open interval from zero to one.</summary>
    /// <param name="buffer">The array to write into.</param>
    /// <param name="offset">Index of the first element to write.</param>
    /// <param name="count">Number of elements to write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range lies outside <paramref name="buffer"/>.</exception>
    public void NextDoubles(double[] buffer, int offset, int count)
    {
        ValidateRange(buffer, offset, count);
        for (var i = 0; i < count; i++)
        {
            buffer[offset + i] = NextDouble();
        }
    }

    /// <summary>Fills an array with values uniform on the open interval from zero to one.</summary>
    /// <param name="buffer">The array to fill.</param>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public void NextDoubles(double[] buffer)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        NextDoubles(buffer, 0, buffer.Length);
    }

    /// <summary>Fills part of an array with integers uniform over a closed range.</summary>
    /// <param name="min">Smallest value that can be produced.</param>
    /// <param name="max">Largest value that can be produced, included in the range.</param>
    /// <param name="buffer">The array to write into.</param>
    /// <param name="offset">Index of the first element to write.</param>
    /// <param name="count">Number of elements to write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range lies outside <paramref name="buffer"/>, or <paramref name="max"/> is below <paramref name="min"/>.</exception>
    public void NextInt32sInclusive(int min, int max, int[] buffer, int offset, int count)
    {
        var outcomes = Int32Count(min, max);
        ValidateRange(buffer, offset, count);
        for (var i = 0; i < count; i++)
        {
            buffer[offset + i] = DrawInt32(min, outcomes);
        }
    }

#if NET8_0_OR_GREATER
    /// <summary>Fills a span with values uniform on the open interval from zero to one.</summary>
    /// <param name="destination">The span to fill.</param>
    public void NextDoubles(Span<double> destination)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = NextDouble();
        }
    }

    /// <summary>Fills a span with integers uniform over a closed range.</summary>
    /// <param name="min">Smallest value that can be produced.</param>
    /// <param name="max">Largest value that can be produced, included in the range.</param>
    /// <param name="destination">The span to fill.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="max"/> is below <paramref name="min"/>.</exception>
    public void NextInt32sInclusive(int min, int max, Span<int> destination)
    {
        var outcomes = Int32Count(min, max);
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = DrawInt32(min, outcomes);
        }
    }
#endif

    /// <summary>Captures everything needed to resume this stream later or elsewhere.</summary>
    /// <returns>A fresh snapshot, safe to serialize.</returns>
    public RandomStreamState SaveState()
    {
        return new RandomStreamState
        {
            Version = RandomStreamState.CurrentVersion,
            Name = _name,
            StreamStart = _streamStart.ToArray(),
            SubstreamStart = _substreamStart.ToArray(),
            Current = _current.ToArray(),
            SubstreamIndex = _substreamIndex,
            Antithetic = _antithetic,
            HighPrecision = _highPrecision,
        };
    }

    /// <summary>Restores this stream from a snapshot, discarding its present position.</summary>
    /// <param name="state">The snapshot to restore.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The snapshot is of an unknown version or holds an invalid state.</exception>
    public void LoadState(RandomStreamState state)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (state.Version != RandomStreamState.CurrentVersion)
        {
            throw new ArgumentException(
                FormattableString.Invariant(
                    $"Unsupported state version {state.Version}; this build reads version {RandomStreamState.CurrentVersion}."),
                nameof(state));
        }

        var streamStart = RequireValid(state.StreamStart, nameof(state), "StreamStart");
        var substreamStart = RequireValid(state.SubstreamStart, nameof(state), "SubstreamStart");
        var current = RequireValid(state.Current, nameof(state), "Current");
        if (state.SubstreamIndex < 0 || state.SubstreamIndex >= SubstreamsPerStream)
        {
            throw new ArgumentException(
                FormattableString.Invariant(
                    $"The SubstreamIndex of the state is invalid. It must be below {SubstreamsPerStream}."),
                nameof(state));
        }

        _streamStart = streamStart;
        _substreamStart = substreamStart;
        _current = current;
        _substreamIndex = state.SubstreamIndex;
        _name = state.Name ?? string.Empty;
        _antithetic = state.Antithetic;
        _highPrecision = state.HighPrecision;
    }

    /// <summary>Creates an independent copy of this stream at its present position.</summary>
    /// <returns>A stream that will produce the same values as this one from now on.</returns>
    public RandomStream Clone()
    {
        var copy = new RandomStream(_streamStart, _name);
        copy._substreamStart = _substreamStart;
        copy._current = _current;
        copy._substreamIndex = _substreamIndex;
        copy._antithetic = _antithetic;
        copy._highPrecision = _highPrecision;
        return copy;
    }

    /// <summary>
    /// Presents this stream as a <see cref="Random"/>, for APIs and libraries that ask for the framework type.
    /// </summary>
    /// <returns>
    /// A new adapter that draws from this stream, so draws through either one advance the same state.
    /// </returns>
    public StreamBackedRandom AsRandom()
    {
        return new StreamBackedRandom(this);
    }

    /// <summary>Returns the name of this stream together with its current state.</summary>
    /// <returns>A single line of text.</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append("RandomStream");
        if (_name.Length != 0)
        {
            builder.Append(' ').Append(_name);
        }

        AppendState(builder, " Current = ", _current);
        return builder.ToString();
    }

    /// <summary>Returns the name of this stream, its three state vectors, its substream index, and its flags.</summary>
    /// <returns>Several lines of text.</returns>
    public string ToDetailedString()
    {
        var builder = new StringBuilder();
        builder.Append("RandomStream");
        if (_name.Length != 0)
        {
            builder.Append(' ').Append(_name);
        }

        builder.Append(Environment.NewLine);
        builder.Append("   antithetic = ").Append(_antithetic ? "true" : "false").Append(Environment.NewLine);
        builder.Append("   highPrecision = ").Append(_highPrecision ? "true" : "false").Append(Environment.NewLine);
        builder.Append("   substreamIndex = ")
            .Append(_substreamIndex.ToString(CultureInfo.InvariantCulture))
            .Append(Environment.NewLine);
        AppendState(builder, "   StreamStart = ", _streamStart);
        builder.Append(Environment.NewLine);
        AppendState(builder, "   SubstreamStart = ", _substreamStart);
        builder.Append(Environment.NewLine);
        AppendState(builder, "   Current = ", _current);
        return builder.ToString();
    }

    /// <summary>
    /// Advances the backbone by one step and returns the raw uniform value, ignoring both flags.
    /// </summary>
    /// <returns>An exact multiple of 1 / (2^32 - 208), strictly between zero and one.</returns>
    /// <remarks>
    /// Each component is reduced as <c>a * x + b * (m - y)</c> so the operand of the modulo is never
    /// negative, and the combination adds m1 through an arithmetic shift rather than a test. Nothing
    /// in this method branches. The largest intermediate is about 9.51e15, well inside a signed 64
    /// bit integer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double NextSample()
    {
        var p1 = ((A12 * _current.S11) + (A13 * (M1 - _current.S10))) % M1;
        _current.S10 = _current.S11;
        _current.S11 = _current.S12;
        _current.S12 = p1;

        var p2 = ((A21 * _current.S22) + (A23 * (M2 - _current.S20))) % M2;
        _current.S20 = _current.S21;
        _current.S21 = _current.S22;
        _current.S22 = p2;

        var difference = (long)p1 - (long)p2;
        var z = difference + (M1Signed & ((difference - 1) >> 63));
        return z * NormalizationFactor;
    }

    /// <summary>Draws two raw values and combines them into one of roughly 53 bits.</summary>
    /// <returns>A value strictly between zero and one.</returns>
    /// <remarks>
    /// Each of the two draws is reflected first when the stream is antithetic, and the weighted
    /// second draw then carries an offset of minus one so the pair still reflects as a whole.
    /// Reflecting the combined value instead would agree to within one unit in the last place but
    /// would not reproduce the RngStreams reference output, which is what this shape is for.
    /// </remarks>
    private double NextHighPrecisionSample()
    {
        var first = NextSample();
        var second = NextSample();

        double u;
        if (_antithetic)
        {
            first = 1.0 - first;
            second = 1.0 - second;
            u = first + ((second - 1.0) * HighPrecisionWeight);
        }
        else
        {
            u = first + (second * HighPrecisionWeight);
        }

        if (u < 0.0)
        {
            u += 1.0;
        }

        if (u >= 1.0)
        {
            u -= 1.0;
        }

        return u <= 0.0 ? HighPrecisionFloor : u;
    }

    /// <summary>Moves the current position by 2^<paramref name="exponent"/> applications of a one-step matrix pair.</summary>
    private void JumpByPowerOfTwo(int exponent, ulong[] step1, ulong[] step2)
    {
        if (exponent < 0 || exponent > 255)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exponent),
                exponent,
                "The exponent must be between 0 and 255 inclusive.");
        }

        _current.Jump(
            ModularMatrix.PowerOfTwoPower(step1, exponent, M1),
            ModularMatrix.PowerOfTwoPower(step2, exponent, M2));
    }

    /// <summary>Checks a closed range of <see cref="int"/> and returns how many values it holds.</summary>
    private static long Int32Count(int min, int max)
    {
        if (max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, "The upper bound must not be below the lower bound.");
        }

        return ((long)max - min) + 1;
    }

    /// <summary>Draws one of <paramref name="count"/> consecutive integers starting at <paramref name="min"/>.</summary>
    private int DrawInt32(int min, long count)
    {
        return (int)(min + ScaleToCount(NextDouble(), count));
    }

    /// <summary>Scales a unit value onto a count of outcomes.</summary>
    /// <param name="u">A value in the open unit interval.</param>
    /// <param name="count">The number of outcomes, at least one.</param>
    /// <returns>A value from zero to <paramref name="count"/> minus one.</returns>
    /// <remarks>
    /// The clamp matters only in high-precision mode, where a draw can sit close enough to one
    /// that the scaled product rounds up to <paramref name="count"/> itself.
    /// </remarks>
    private static long ScaleToCount(double u, long count)
    {
        var offset = (long)(count * u);
        return offset >= count ? count - 1 : offset;
    }

    private static StreamStateVector RequireValid(uint[] values, string parameterName, string field)
    {
        if (!StreamStateVector.Validate(values, out var error))
        {
            throw new ArgumentException(
                FormattableString.Invariant($"The {field} vector of the state is invalid. {error}"),
                parameterName);
        }

        return StreamStateVector.FromArray(values);
    }

    private static void ValidateRange<T>(T[] buffer, int offset, int count)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "The offset must not be negative.");
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "The count must not be negative.");
        }

        if (offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                "The requested range extends past the end of the buffer.");
        }
    }

    private static void AppendState(StringBuilder builder, string label, in StreamStateVector state)
    {
        builder.Append(label);
        new Mrg32k3aState(state).AppendTo(builder);
    }
}
