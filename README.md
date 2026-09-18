# Mrg32k3a.NET

A pseudo-random number generator for .NET that provides many independent streams of random
numbers. It is based on Pierre L'Ecuyer's **MRG32k3a** combined multiple recursive generator, with
streams and substreams as described by L'Ecuyer, Simard, Chen and Kelton (2002). It is written
entirely in C# and has no dependencies.

- Independent streams of 2^127 values, each split into 2^51 substreams of 2^76 values
- Jump to any stream or substream by index in time logarithmic in the index, not linear
- Bit-for-bit reproducible across netstandard2.0, net8.0 and net10.0, and across x64 and ARM64
- Rewinds for common random numbers, antithetic variates, and 53-bit precision draws
- Serializable stream and factory snapshots, so a run resumes without reissuing live streams
- A `System.Random` adapter

## Install

```bash
dotnet add package Mrg32k3a.NET
```

## Example

```csharp
using Mrg32k3a.NET;

var factory = new RandomStreamFactory();
var arrivals = factory.CreateStream("arrivals");
var service = factory.CreateStream("service");

for (var replication = 0; replication < 100; replication++)
{
    double u = arrivals.NextDouble();
    int k = service.NextInt32Inclusive(1, 6);
    // ...

    arrivals.SkipToNextSubstream();
    service.SkipToNextSubstream();
}
```

## Upgrading from 0.1.0

Stream snapshots moved to version 2 to carry the substream index, which cannot be recovered from
the state vectors. `LoadState` and `FromState` refuse a version 1 snapshot rather than loading it
with a substituted index.

Version 2 adds that one property and changes nothing else, so a saved version 1 document can be
migrated by hand: set `Version` to 2 and add `SubstreamIndex`, the substream the stream was on. The
three state vectors carry over untouched, so the restored stream resumes at exactly the position it
was saved at. If that number was not recorded, it cannot be recovered from the snapshot, because the
seed and stream index reach only the start of a stream and not a position inside it; take a version
2 snapshot before upgrading, or resume from the start of the stream.

Nothing else changed: the values a stream produces are unmoved.

## Documentation

Guides and the API reference are at **https://simverk.github.io/Mrg32k3a.NET**.

## License

Apache License 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).
