namespace Mrg32k3a.NET;

/// <summary>
/// A transferable snapshot of everything a <see cref="RandomStream"/> needs in order to resume: its
/// initial state, the start of its current substream, its current state, the index of that
/// substream, and its two output flags.
/// </summary>
/// <remarks>
/// <para>
/// The type is a plain mutable object with a parameterless constructor and public properties, so any
/// general purpose serializer can handle it. The library deliberately takes no serialization
/// dependency, which means the property names are used verbatim unless the caller applies a naming
/// policy of their own.
/// </para>
/// <para>
/// <see cref="Version"/> identifies the shape of the snapshot. <see cref="RandomStream.LoadState"/>
/// accepts only <see cref="CurrentVersion"/> and rejects every other value. Version 2 added
/// <see cref="SubstreamIndex"/>, which cannot be recovered from the state vectors, so a version 1
/// snapshot is rejected rather than loaded with a substituted index.
/// </para>
/// </remarks>
public sealed class RandomStreamState
{
    /// <summary>The snapshot version this build writes.</summary>
    public const int CurrentVersion = 2;

    /// <summary>Gets or sets the snapshot version.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Gets or sets the label carried by the stream.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the initial state of the stream, six unsigned integers.</summary>
    public uint[] StreamStart { get; set; } = new uint[StreamStateVector.Length];

    /// <summary>Gets or sets the state at the start of the current substream, six unsigned integers.</summary>
    public uint[] SubstreamStart { get; set; } = new uint[StreamStateVector.Length];

    /// <summary>Gets or sets the current state of the stream, six unsigned integers.</summary>
    public uint[] Current { get; set; } = new uint[StreamStateVector.Length];

    /// <summary>Gets or sets the zero-based index of the substream the stream is inside.</summary>
    /// <remarks>Must be below the 2^51 substreams a stream holds, or the snapshot is refused.</remarks>
    public long SubstreamIndex { get; set; }

    /// <summary>Gets or sets whether the stream returns antithetic variates.</summary>
    public bool Antithetic { get; set; }

    /// <summary>Gets or sets whether the stream returns high-precision variates.</summary>
    public bool HighPrecision { get; set; }
}
