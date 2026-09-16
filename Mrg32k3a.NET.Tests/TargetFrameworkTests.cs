using System.Reflection;
using System.Runtime.Versioning;

namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Checks that each test run exercises the library build and runtime it is named for, so a pass
/// on one target cannot stand in for another.
/// </summary>
public class TargetFrameworkTests
{
    [Fact]
    public void TestsLoadTheIntendedLibraryBuild()
    {
#if LIBRARY_NETSTANDARD2_0
        const string expected = ".NETStandard,Version=v2.0";
#elif NET10_0
        const string expected = ".NETCoreApp,Version=v10.0";
#elif NET8_0
        const string expected = ".NETCoreApp,Version=v8.0";
#endif
        var attribute = typeof(RandomStream).Assembly.GetCustomAttribute<TargetFrameworkAttribute>();

        Assert.Equal(expected, attribute?.FrameworkName);
    }

    [Fact]
    public void TestsRunOnTheRuntimeTheyTarget()
    {
#if NET10_0
        const int expected = 10;
#elif NET8_0
        const int expected = 8;
#endif

        Assert.Equal(expected, Environment.Version.Major);
    }
}
