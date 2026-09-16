---
_layout: landing
---

# Mrg32k3a.NET

Mrg32k3a.NET is a pseudo-random number generator for .NET that provides many independent streams
of random numbers. It is based on Pierre L'Ecuyer's **MRG32k3a** combined multiple recursive
generator ([L'Ecuyer, 1999](https://doi.org/10.1287/opre.47.1.159)), with streams and substreams
as described by
[L'Ecuyer, Simard, Chen and Kelton (2002)](https://doi.org/10.1287/opre.50.6.1073.358). It is
written entirely in C# and has no dependencies.

It is built for stochastic simulation and Monte Carlo work, where you need:

- **Many independent streams.** Each stream is its own block of 2^127 values, split into 2^51
  substreams of 2^76 values each.
- **Exact reproducibility.** The same seed gives the same sequence on every target framework
  (netstandard2.0, net8.0, net10.0) and on both x64 and ARM64.
- **Common random numbers and antithetic variates**, using stream and substream rewinds and the
  `Antithetic` flag.

## Install

```bash
dotnet add package Mrg32k3a.NET
```

## Quick example

```csharp
using Mrg32k3a.NET;

var factory = new RandomStreamFactory();
var arrivals = factory.CreateStream("arrivals");
var service = factory.CreateStream("service");

double u = arrivals.NextDouble();         // uniform on (0, 1)
int die = service.NextInt32Inclusive(1, 6);
```

## Next steps

- [Getting started](articles/getting-started.md)
- [Multiple streams](articles/multiple-streams.md)
- [Reproducibility and state](articles/reproducibility.md)
- [API reference](api/Mrg32k3a.NET.yml)
