namespace Mrg32k3a.NET;

/// <summary>
/// Numeric constants of the MRG32k3a backbone generator and of the stream / substream
/// partitioning built on top of it.
/// </summary>
/// <remarks>
/// The recurrence constants are those published by L'Ecuyer (1999). The stream and substream
/// lengths, the default seed and the high-precision weight follow L'Ecuyer, Simard, Chen and
/// Kelton (2002), "An Object-Oriented Random-Number Package with Many Long Streams and
/// Substreams". The jump matrices are the
/// transition matrices of the two components raised to the powers 2^76 and 2^127 modulo the
/// corresponding modulus and reproduced here so that streams do not have to recalculate them.
/// The backward tables are the corresponding inverses, which let a stream move towards its start
/// as cheaply as it moves away from it.
/// <c>ModularMatrixTests</c> guards against drift by verifying they match values recalculated
/// from <see cref="A1"/> and <see cref="A2"/>.
/// </remarks>
internal static class Mrg32k3aConstants
{
    /// <summary>Modulus of the first component, 2^32 - 209.</summary>
    internal const ulong M1 = 4294967087UL;

    /// <summary>Modulus of the second component, 2^32 - 22853.</summary>
    internal const ulong M2 = 4294944443UL;

    /// <summary><see cref="M1"/> as a signed value, for the branch-free output combination.</summary>
    internal const long M1Signed = 4294967087L;

    /// <summary>Coefficient of x1[n-2] in the first component.</summary>
    internal const ulong A12 = 1403580UL;

    /// <summary>Magnitude of the (negative) coefficient of x1[n-3] in the first component.</summary>
    internal const ulong A13 = 810728UL;

    /// <summary>Coefficient of x2[n-1] in the second component.</summary>
    internal const ulong A21 = 527612UL;

    /// <summary>Magnitude of the (negative) coefficient of x2[n-3] in the second component.</summary>
    internal const ulong A23 = 1370589UL;

    /// <summary>
    /// Normalization factor 1 / (<see cref="M1"/> + 1), as the correctly rounded double.
    /// </summary>
    /// <remarks>
    /// The output must be produced by multiplying by this constant, never by dividing by
    /// 4294967088.0. The two disagree by one unit in the last place for a large fraction of
    /// states, and the reference implementation in L'Ecuyer (1999), Figure 1, multiplies.
    /// </remarks>
    internal const double NormalizationFactor = 2.328306549295727688e-10;

    /// <summary>Weight 2^-24 applied to the second draw of a high-precision output.</summary>
    internal const double HighPrecisionWeight = 5.9604644775390625e-8;

    /// <summary>The resolution of a high-precision output, used as its positive floor.</summary>
    internal const double HighPrecisionFloor = NormalizationFactor * HighPrecisionWeight;

    /// <summary>Value of every component of the default factory seed.</summary>
    internal const uint DefaultSeedComponent = 12345U;

    /// <summary>Base-two logarithm of the substream length.</summary>
    internal const int SubstreamExponent = 76;

    /// <summary>Base-two logarithm of the stream length.</summary>
    internal const int StreamExponent = 127;

    /// <summary>Number of substreams inside one stream, 2^51.</summary>
    /// <remarks>
    /// Derived from the two exponents rather than written out, so it cannot drift away from the
    /// partitioning they describe.
    /// </remarks>
    internal const long SubstreamsPerStream = 1L << (StreamExponent - SubstreamExponent);

    /// <summary>One-step transition matrix of the first component, row major.</summary>
    internal static readonly ulong[] A1 =
    {
        0, 1, 0,
        0, 0, 1,
        M1 - A13, A12, 0,
    };

    /// <summary>One-step transition matrix of the second component, row major.</summary>
    internal static readonly ulong[] A2 =
    {
        0, 1, 0,
        0, 0, 1,
        M2 - A23, 0, A21,
    };

    /// <summary>First component advanced by 2^76 steps, the substream jump.</summary>
    internal static readonly ulong[] A1P76 =
    {
        82758667, 1871391091, 4127413238,
        3672831523, 69195019, 1871391091,
        3672091415, 3528743235, 69195019,
    };

    /// <summary>Second component advanced by 2^76 steps, the substream jump.</summary>
    internal static readonly ulong[] A2P76 =
    {
        1511326704, 3759209742, 1610795712,
        4292754251, 1511326704, 3889917532,
        3859662829, 4292754251, 3708466080,
    };

    /// <summary>First component advanced by 2^127 steps, the stream jump.</summary>
    internal static readonly ulong[] A1P127 =
    {
        2427906178, 3580155704, 949770784,
        226153695, 1230515664, 3580155704,
        1988835001, 986791581, 1230515664,
    };

    /// <summary>Second component advanced by 2^127 steps, the stream jump.</summary>
    internal static readonly ulong[] A2P127 =
    {
        1464411153, 277697599, 1610723613,
        32183930, 1464411153, 1022607788,
        2824425944, 32183930, 2093834863,
    };

    /// <summary>Inverse of <see cref="A1"/> modulo <see cref="M1"/>, the one-step backward jump.</summary>
    internal static readonly ulong[] InvA1 =
    {
        184888585, 0, 1945170933,
        1, 0, 0,
        0, 1, 0,
    };

    /// <summary>Inverse of <see cref="A2"/> modulo <see cref="M2"/>, the one-step backward jump.</summary>
    internal static readonly ulong[] InvA2 =
    {
        0, 360363334, 4225571728,
        1, 0, 0,
        0, 1, 0,
    };

    /// <summary>Inverse of <see cref="A1P76"/> modulo <see cref="M1"/>, the backward substream jump.</summary>
    internal static readonly ulong[] InvA1P76 =
    {
        2585822061, 2346541846, 600781890,
        42385315, 4257896290, 2346541846,
        1248824805, 2390631828, 4257896290,
    };

    /// <summary>Inverse of <see cref="A2P76"/> modulo <see cref="M2"/>, the backward substream jump.</summary>
    internal static readonly ulong[] InvA2P76 =
    {
        855407695, 4134906251, 112088500,
        2897599610, 855407695, 1987588141,
        854109890, 2897599610, 1099731892,
    };
}
