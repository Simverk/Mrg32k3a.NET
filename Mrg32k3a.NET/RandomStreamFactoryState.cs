namespace Mrg32k3a.NET;

/// <summary>
/// A transferable snapshot of everything a <see cref="RandomStreamFactory"/> needs in order to
/// resume: its seed, and how many streams it has already handed out.
/// </summary>
/// <remarks>
/// <para>
/// The type is a plain mutable object with a parameterless constructor and public properties, so any
/// general purpose serializer can handle it. The library deliberately takes no serialization
/// dependency, which means the property names are used verbatim unless the caller applies a naming
/// policy of their own.
/// </para>
/// <para>
/// Saving a run means saving this snapshot as well as the streams the factory has handed out.
/// Without it a reloaded run starts its factory over at the first stream, which hands out streams
/// that are already in use and gives two workers the same values.
/// </para>
/// <para>
/// <see cref="Version"/> identifies the shape of the snapshot.
/// <see cref="RandomStreamFactory.FromState"/> accepts only <see cref="CurrentVersion"/> and rejects
/// every other value.
/// </para>
/// </remarks>
public sealed class RandomStreamFactoryState
{
    /// <summary>The snapshot version this build writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Gets or sets the snapshot version.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets or sets the seed of the factory, six unsigned integers.</summary>
    /// <remarks>
    /// This is the start of the factory's first stream, not of its next one. The default is six
    /// zeros, which breaks the seed rules and is rejected, so a snapshot has to be filled in before
    /// it can be restored.
    /// </remarks>
    public uint[] Seed { get; set; } = new uint[StreamStateVector.Length];

    /// <summary>Gets or sets the number of streams the factory has handed out.</summary>
    /// <remarks>
    /// It is a count rather than an index, so the restored factory's next stream is the one at this
    /// position in its ordering, the same one <see cref="RandomStreamFactory.CreateStreamAt"/> builds
    /// for this value.
    /// </remarks>
    public long CreatedStreamCount { get; set; }
}
