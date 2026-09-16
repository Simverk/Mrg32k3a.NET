#if NETSTANDARD2_0
namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Stand-in for the framework attribute of the same name, which netstandard2.0 does not ship. The
/// compiler matches it by full name, so it carries the same nullable analysis on every target.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property)]
internal sealed class AllowNullAttribute : Attribute
{
}
#endif
