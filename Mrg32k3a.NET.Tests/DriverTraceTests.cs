using System.Globalization;
using System.Text.Json;

namespace Mrg32k3a.NET.Tests;

/// <summary>
/// Replays the reference driver step by step and checks this implementation against it at every
/// point: the state of each stream after each operation, every accumulator after each step, and the
/// final checksum the driver prints.
/// </summary>
/// <remarks>
/// This is the case that could not be reproduced from the earlier vector packages, because the total
/// is a single accumulated double and floating-point addition depends on the order terms arrive. The
/// trace supplies that order as data.
/// </remarks>
public class DriverTraceTests
{
    private readonly Dictionary<string, RandomStream> _streams = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _doubles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _integers = new(StringComparer.Ordinal);
    private RandomStreamFactory _factory = new();

    [Fact]
    public void ReplayingTheTraceReproducesEveryStepAndTheFinalChecksum()
    {
        var trace = ReferenceVectors.Case("testRngStream_driver");
        _factory = new RandomStreamFactory(new Mrg32k3aState(trace.PackageSeed!));

        ClassifyAccumulators(trace);

        foreach (var step in trace.Steps!)
        {
            Execute(step);
            VerifyState(step);
            VerifyAccumulators(step);
        }

        var final = trace.Final!;
        var printed = _doubles[final.Accumulator];

        ReferenceVectors.AssertSameBits(final.ValueBits, printed, "final accumulator");
        Assert.Equal(final.Value, printed);
        Assert.Equal("39.697547", printed.ToString("F6", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TheTraceClosesTheStandaloneChecksumCase()
    {
        // The trace's closing value is the case that used to be carried as an exception.
        var trace = ReferenceVectors.Case("testRngStream_driver");
        var standalone = ReferenceVectors.Case("checksum_testRngStream_final");

        Assert.Equal(standalone.ValueBits, trace.Final!.ValueBits);
        Assert.Equal(standalone.Value!.Value, trace.Final.Value);
    }

    [Fact]
    public void TheTraceExercisesEveryOperationTheReplaySupports()
    {
        // Guards the replay rather than the arithmetic. If a future trace stopped emitting flag
        // steps or substream resets, the replay would still pass while covering less, and this is
        // what notices.
        var trace = ReferenceVectors.Case("testRngStream_driver");
        var operations = trace.Steps!.Select(step => step.Op).ToHashSet(StringComparer.Ordinal);

        Assert.Superset(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "create_stream", "set_package_seed", "reset_start_stream", "reset_start_substream",
                "reset_next_substream", "set_antithetic", "increased_precis", "advance_state",
                "rand_u01", "loop", "combine",
            },
            operations);

        // Both flags must actually be switched on somewhere, and a backward jump must occur.
        Assert.Contains(trace.Steps!, s => s.Op == "set_antithetic" && s.Args!["value"].GetInt32() == 1);
        Assert.Contains(trace.Steps!, s => s.Op == "increased_precis" && s.Args!["value"].GetInt32() == 1);
        Assert.Contains(trace.Steps!, s => s.Op == "advance_state" && s.Args!["e"].GetInt32() < 0);
        Assert.Contains(trace.Steps!, s => s.Op == "loop" && s.Args!["body"].GetString() == "rand_int");
    }

    private void ClassifyAccumulators(VectorCase trace)
    {
        var first = trace.Steps![0];
        foreach (var name in trace.Accumulators!)
        {
            if (first.AccumulatorBits[name].ValueKind == JsonValueKind.String)
            {
                _doubles[name] = 0.0;
            }
            else
            {
                _integers[name] = 0L;
            }
        }
    }

    private void Execute(TraceStep step)
    {
        var args = step.Args ?? new Dictionary<string, JsonElement>();

        switch (step.Op)
        {
            case "create_stream":
                _streams[step.Stream!] = _factory.CreateStream(step.Stream);
                break;

            case "set_package_seed":
                // Setting the package seed restarts creation order and leaves existing streams alone,
                // which is exactly what replacing the factory does.
                _factory = new RandomStreamFactory(
                    new Mrg32k3aState(args["seed"].EnumerateArray().Select(e => e.GetUInt32()).ToArray()));
                break;

            case "reset_start_stream":
                Stream(step).RewindStream();
                break;

            case "reset_start_substream":
                Stream(step).RewindSubstream();
                break;

            case "reset_next_substream":
                Stream(step).SkipToNextSubstream();
                break;

            case "set_antithetic":
                Stream(step).Antithetic = args["value"].GetInt32() != 0;
                break;

            case "increased_precis":
                Stream(step).HighPrecision = args["value"].GetInt32() != 0;
                break;

            case "advance_state":
                Advance(Stream(step), args["e"].GetInt32(), args["c"].GetInt64());
                break;

            case "rand_u01":
            {
                var draw = Stream(step).NextDouble();
                ReferenceVectors.AssertSameBits(step.DrawBits!, draw, $"step {step.Index} draw");
                AddTo(args, draw);
                break;
            }

            case "loop":
                RunLoop(Stream(step), step, args);
                break;

            case "combine":
                Combine(args);
                break;

            default:
                throw new InvalidOperationException($"Step {step.Index} uses unknown operation '{step.Op}'.");
        }
    }

    private void RunLoop(RandomStream stream, TraceStep step, Dictionary<string, JsonElement> args)
    {
        var count = args["count"].GetInt32();
        var body = args["body"].GetString();
        var into = args.TryGetValue("into", out var target) && target.ValueKind != JsonValueKind.Null
            ? target.GetString()
            : null;

        switch (body)
        {
            case "advance_state":
            {
                var e = args["e"].GetInt32();
                var c = args["c"].GetInt64();
                for (var i = 0; i < count; i++)
                {
                    Advance(stream, e, c);
                }

                break;
            }

            case "rand_u01":
            {
                var total = args["init"].GetDouble();
                for (var i = 0; i < count; i++)
                {
                    total += stream.NextDouble();
                }

                ReferenceVectors.AssertSameBits(step.BlockTotalBits!, total, $"step {step.Index} block total");
                _doubles[into!] = total;
                break;
            }

            case "rand_int":
            {
                var lo = args["lo"].GetInt64();
                var hi = args["hi"].GetInt64();
                var total = args["init"].GetInt64();
                for (var i = 0; i < count; i++)
                {
                    total += stream.NextInt64Inclusive(lo, hi);
                }

                Assert.Equal(step.BlockTotal!.Value.GetInt64(), total);
                _integers[into!] = total;
                break;
            }

            default:
                throw new InvalidOperationException($"Step {step.Index} loops over unknown body '{body}'.");
        }
    }

    private void Combine(Dictionary<string, JsonElement> args)
    {
        var target = args["target"].GetString()!;
        var source = args["source"].GetString()!;
        var operation = args["operation"].GetString();

        var operand = args.TryGetValue("operand_bits", out var bits)
            ? ReferenceVectors.FromBits(bits.GetString()!)
            : args["operand"].GetDouble();

        var value = _doubles.TryGetValue(source, out var asDouble) ? asDouble : _integers[source];

        _doubles[target] += operation switch
        {
            "divide_then_add" => value / operand,
            _ => throw new InvalidOperationException($"Unknown combine operation '{operation}'."),
        };
    }

    private static void Advance(RandomStream stream, int exponent, long offset)
    {
        if (exponent > 0)
        {
            stream.AdvanceByPowerOfTwo(exponent);
        }
        else if (exponent < 0)
        {
            stream.RetreatByPowerOfTwo(-exponent);
        }

        if (offset != 0)
        {
            stream.Advance(offset);
        }
    }

    private void AddTo(Dictionary<string, JsonElement> args, double draw)
    {
        if (args.TryGetValue("into", out var target) && target.ValueKind == JsonValueKind.String)
        {
            _doubles[target.GetString()!] += draw;
        }
    }

    private void VerifyState(TraceStep step)
    {
        if (step.CgAfter is not null)
        {
            Assert.Equal(step.CgAfter, Stream(step).CurrentState.ToArray());
        }
    }

    private void VerifyAccumulators(TraceStep step)
    {
        foreach (var entry in step.AccumulatorBits)
        {
            if (entry.Value.ValueKind == JsonValueKind.String)
            {
                ReferenceVectors.AssertSameBits(
                    entry.Value.GetString()!,
                    _doubles[entry.Key],
                    $"step {step.Index} accumulator {entry.Key}");
            }
            else
            {
                Assert.Equal(entry.Value.GetInt64(), _integers[entry.Key]);
            }
        }
    }

    private RandomStream Stream(TraceStep step)
    {
        return _streams[step.Stream ?? throw new InvalidOperationException($"Step {step.Index} names no stream.")];
    }
}
