using System.Globalization;
using System.Reflection;

namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Compares the library against a table of values derived independently from the recurrence
/// of L'Ecuyer (1999). The table is checked in, so any change of behaviour on any target framework or any
/// processor architecture fails the build rather than passing quietly.
/// </summary>
public class DeterminismTests
{
    [Fact]
    public void FirstDrawsOfTheFirstThreeStreamsMatchTheCheckedInTable()
    {
        var expected = LoadGoldenVectors();
        var factory = new RandomStreamFactory();

        for (var ordinal = 1; ordinal <= 3; ordinal++)
        {
            var stream = factory.CreateStream();
            var values = expected[ordinal];

            for (var draw = 0; draw < values.Count; draw++)
            {
                Assert.Equal(values[draw], stream.NextDouble());
            }
        }
    }

    [Fact]
    public void TheCheckedInTableIsComplete()
    {
        var expected = LoadGoldenVectors();

        Assert.Equal(3, expected.Count);
        Assert.All(expected.Values, values => Assert.Equal(200, values.Count));
    }

    private static Dictionary<int, List<double>> LoadGoldenVectors()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith("GoldenVectors.txt", StringComparison.Ordinal));
        Assert.NotNull(name);

        using var resource = assembly.GetManifestResourceStream(name!)!;
        using var reader = new StreamReader(resource);

        var result = new Dictionary<int, List<double>>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var fields = line.Split(',');
            var ordinal = int.Parse(fields[0], CultureInfo.InvariantCulture);
            var value = double.Parse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture);

            if (!result.TryGetValue(ordinal, out var values))
            {
                values = new List<double>();
                result[ordinal] = values;
            }

            values.Add(value);
        }

        return result;
    }
}
