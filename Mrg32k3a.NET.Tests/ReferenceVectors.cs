using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Loads the verification vectors produced by the RngStreams C reference and shipped alongside these
/// tests.
/// </summary>
/// <remarks>
/// The archive and the JSON inside it are both hashed on every run so a silently altered artifact
/// cannot weaken the comparison.
/// </remarks>
internal static class ReferenceVectors
{
    /// <summary>Hash of the shipped archive, from <c>rngstreams-vectors.tar.gz.sha256</c>.</summary>
    internal const string ArchiveSha256 = "7d969a604088037709af57fb06e74da13cfe07bf014940e54b04f9d994437691";

    /// <summary>Hash of the JSON inside the archive, from <c>vectors.json.sha256</c>.</summary>
    internal const string VectorsSha256 = "8c552116f5ae310b9fd674f9d3a35ea80e5af94acf72630b5a0b49a29247c35b";

    /// <summary>
    /// Environment variable naming an uncompressed vectors file to use instead of the shipped
    /// archive, so the vectors can be regenerated at a larger draw count for a deeper comparison.
    /// </summary>
    internal const string OverrideVariable = "MRG32K3A_REFERENCE_VECTORS";

    private static readonly Lazy<VectorFile> Loaded = new(Load);

    /// <summary>Gets the parsed vector file.</summary>
    internal static VectorFile File => Loaded.Value;

    /// <summary>Gets whether the vectors came from the shipped, hash-pinned archive.</summary>
    internal static bool UsingShippedArchive { get; private set; }

    /// <summary>Returns the case with the given name.</summary>
    /// <param name="name">The case name from the vector file.</param>
    /// <returns>The matching case.</returns>
    internal static VectorCase Case(string name)
    {
        return Array.Find(File.Cases, c => c.Name == name)
               ?? throw new InvalidOperationException($"The vector file has no case named '{name}'.");
    }

    /// <summary>Returns the names of every case of a given kind.</summary>
    /// <param name="kind">The case kind, such as <c>u01_sequence</c>.</param>
    /// <returns>The matching case names in file order.</returns>
    internal static IEnumerable<string> NamesOfKind(string kind)
    {
        return File.Cases.Where(c => c.Kind == kind).Select(c => c.Name);
    }

    private static VectorFile Load()
    {
        var json = ReadJson();
        var parsed = JsonSerializer.Deserialize<VectorFile>(json)
                     ?? throw new InvalidOperationException("The vector file parsed as null.");
        return parsed;
    }

    private static byte[] ReadJson()
    {
        var overridePath = Environment.GetEnvironmentVariable(OverrideVariable);
        if (!string.IsNullOrEmpty(overridePath))
        {
            UsingShippedArchive = false;
            return System.IO.File.ReadAllBytes(overridePath);
        }

        var archive = Path.Combine(AppContext.BaseDirectory, "rngstreams-vectors.tar.gz");
        var archiveBytes = System.IO.File.ReadAllBytes(archive);
        if (!HashOf(archiveBytes).Equals(ArchiveSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The reference archive does not match its published hash.");
        }

        using var compressed = new MemoryStream(archiveBytes, writable: false);
        using var decompressed = new GZipStream(compressed, CompressionMode.Decompress);
        using var tar = new TarReader(decompressed);

        while (tar.GetNextEntry() is { } entry)
        {
            if (!entry.Name.EndsWith("vectors.json", StringComparison.Ordinal) || entry.DataStream is null)
            {
                continue;
            }

            using var buffer = new MemoryStream();
            entry.DataStream.CopyTo(buffer);
            var json = buffer.ToArray();

            if (!HashOf(json).Equals(VectorsSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The reference vectors do not match their published hash.");
            }

            UsingShippedArchive = true;
            return json;
        }

        throw new InvalidOperationException("The reference archive does not contain vectors.json.");
    }

    /// <summary>Parses a hex bit pattern such as <c>0x3fc041e683b58b4b</c> into a double.</summary>
    /// <param name="bits">The pattern, with or without a leading <c>0x</c>.</param>
    /// <returns>The double with exactly those bits.</returns>
    internal static double FromBits(string bits)
    {
        var text = bits.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? bits.Substring(2) : bits;
        return BitConverter.Int64BitsToDouble(
            unchecked((long)ulong.Parse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>Asserts that a value has exactly the bit pattern the reference recorded.</summary>
    /// <param name="expectedBits">The reference pattern.</param>
    /// <param name="actual">The value produced here.</param>
    /// <param name="what">Label used in the failure message.</param>
    internal static void AssertSameBits(string expectedBits, double actual, string what)
    {
        var expected = BitConverter.DoubleToInt64Bits(FromBits(expectedBits));
        var produced = BitConverter.DoubleToInt64Bits(actual);

        if (expected != produced)
        {
            throw new Xunit.Sdk.XunitException(
                $"{what}: expected {expectedBits} but produced 0x{produced:x16} ({actual:R})");
        }
    }

    private static string HashOf(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }
}

/// <summary>Top level of the reference vector file.</summary>
internal sealed class VectorFile
{
    /// <summary>Gets or sets the provenance block.</summary>
    [JsonPropertyName("meta")]
    public VectorMeta Meta { get; set; } = new();

    /// <summary>Gets or sets the verification cases.</summary>
    [JsonPropertyName("cases")]
    public VectorCase[] Cases { get; set; } = Array.Empty<VectorCase>();
}

/// <summary>Provenance recorded by the generator.</summary>
internal sealed class VectorMeta
{
    /// <summary>Gets or sets the description of the reference implementation.</summary>
    [JsonPropertyName("generator")]
    public string Generator { get; set; } = string.Empty;

    /// <summary>Gets or sets the draw count used for the long sequences.</summary>
    [JsonPropertyName("n_core")]
    public int CoreDraws { get; set; }

    /// <summary>Gets or sets the draw count used for the named-stream sequences.</summary>
    [JsonPropertyName("n_named")]
    public int NamedDraws { get; set; }
}

/// <summary>One verification case.</summary>
internal sealed class VectorCase
{
    /// <summary>Gets or sets the case kind.</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the case name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the human readable description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the state the stream must be placed in before drawing.</summary>
    [JsonPropertyName("initial_cg")]
    public uint[]? InitialCg { get; set; }

    /// <summary>Gets or sets whether the stream generates antithetic variates.</summary>
    [JsonPropertyName("antithetic")]
    public int? Antithetic { get; set; }

    /// <summary>Gets or sets whether the stream generates high-precision variates.</summary>
    [JsonPropertyName("increased_precis")]
    public int? HighPrecision { get; set; }

    /// <summary>Gets or sets the lower bound of an integer draw.</summary>
    [JsonPropertyName("lo")]
    public long? Low { get; set; }

    /// <summary>Gets or sets the upper bound of an integer draw, included in the range.</summary>
    [JsonPropertyName("hi")]
    public long? High { get; set; }

    /// <summary>Gets or sets the recorded draws.</summary>
    [JsonPropertyName("draws")]
    public VectorDraw[]? Draws { get; set; }

    /// <summary>Gets or sets the state the reference produced.</summary>
    [JsonPropertyName("actual")]
    public uint[]? Actual { get; set; }

    /// <summary>Gets or sets the published value the reference was compared against.</summary>
    [JsonPropertyName("expected")]
    public JsonElement? Expected { get; set; }

    /// <summary>Gets or sets whether the reference agreed with the published value.</summary>
    [JsonPropertyName("match")]
    public bool? Match { get; set; }

    /// <summary>Gets or sets a scalar result.</summary>
    [JsonPropertyName("value")]
    public double? Value { get; set; }

    /// <summary>Gets or sets the bit pattern of <see cref="Value"/>.</summary>
    [JsonPropertyName("value_bits")]
    public string? ValueBits { get; set; }

    /// <summary>Gets or sets the package seed a driver trace starts from.</summary>
    [JsonPropertyName("package_seed")]
    public uint[]? PackageSeed { get; set; }

    /// <summary>Gets or sets the names of the accumulators a driver trace tracks.</summary>
    [JsonPropertyName("accumulators")]
    public string[]? Accumulators { get; set; }

    /// <summary>Gets or sets the ordered steps of a driver trace.</summary>
    [JsonPropertyName("steps")]
    public TraceStep[]? Steps { get; set; }

    /// <summary>Gets or sets the closing result of a driver trace.</summary>
    [JsonPropertyName("final")]
    public TraceFinal? Final { get; set; }
}

/// <summary>One step of a driver trace.</summary>
internal sealed class TraceStep
{
    /// <summary>Gets or sets the step ordinal.</summary>
    [JsonPropertyName("i")]
    public int Index { get; set; }

    /// <summary>Gets or sets the operation performed.</summary>
    [JsonPropertyName("op")]
    public string Op { get; set; } = string.Empty;

    /// <summary>Gets or sets the stream acted on, or null for package-level and arithmetic steps.</summary>
    [JsonPropertyName("stream")]
    public string? Stream { get; set; }

    /// <summary>Gets or sets the operation arguments.</summary>
    [JsonPropertyName("args")]
    public Dictionary<string, JsonElement>? Args { get; set; }

    /// <summary>Gets or sets the state of the named stream after the step.</summary>
    [JsonPropertyName("cg_after")]
    public uint[]? CgAfter { get; set; }

    /// <summary>Gets or sets the bit pattern of a single draw the step produced.</summary>
    [JsonPropertyName("draw_bits")]
    public string? DrawBits { get; set; }

    /// <summary>Gets or sets the subtotal a loop step produced, before any scaling.</summary>
    [JsonPropertyName("block_total")]
    public JsonElement? BlockTotal { get; set; }

    /// <summary>Gets or sets the bit pattern of a loop subtotal that feeds a double accumulator.</summary>
    [JsonPropertyName("block_total_bits")]
    public string? BlockTotalBits { get; set; }

    /// <summary>Gets or sets every accumulator after the step.</summary>
    [JsonPropertyName("acc")]
    public Dictionary<string, JsonElement> Accumulators { get; set; } = new();

    /// <summary>
    /// Gets or sets the same accumulators, as bit patterns for the double ones and as plain integers
    /// for the integer ones.
    /// </summary>
    [JsonPropertyName("acc_bits")]
    public Dictionary<string, JsonElement> AccumulatorBits { get; set; } = new();
}

/// <summary>The closing result of a driver trace.</summary>
internal sealed class TraceFinal
{
    /// <summary>Gets or sets the accumulator the driver prints.</summary>
    [JsonPropertyName("accumulator")]
    public string Accumulator { get; set; } = string.Empty;

    /// <summary>Gets or sets the printed value.</summary>
    [JsonPropertyName("value")]
    public double Value { get; set; }

    /// <summary>Gets or sets the bit pattern of the printed value.</summary>
    [JsonPropertyName("value_bits")]
    public string ValueBits { get; set; } = string.Empty;

    /// <summary>Gets or sets the format the driver prints with.</summary>
    [JsonPropertyName("printf")]
    public string Printf { get; set; } = string.Empty;
}

/// <summary>One recorded draw.</summary>
internal sealed class VectorDraw
{
    /// <summary>Gets or sets the draw ordinal.</summary>
    [JsonPropertyName("i")]
    public int Index { get; set; }

    /// <summary>Gets or sets the stream state after the draw.</summary>
    [JsonPropertyName("cg")]
    public uint[] Cg { get; set; } = Array.Empty<uint>();

    /// <summary>Gets or sets the decimal rendering of a uniform draw.</summary>
    [JsonPropertyName("u01")]
    public double? U01 { get; set; }

    /// <summary>Gets or sets the bit pattern of a uniform draw.</summary>
    [JsonPropertyName("u01_bits")]
    public string? U01Bits { get; set; }

    /// <summary>Gets or sets an integer draw.</summary>
    [JsonPropertyName("value")]
    public long? Value { get; set; }
}
