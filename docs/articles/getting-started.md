# Getting started

## Create a factory, then take streams from it

Every stream comes from a <xref:Mrg32k3a.NET.RandomStreamFactory>. A new factory uses the default
seed of L'Ecuyer et al. (2002), which is six copies of 12345. The first stream starts at that seed, and each
later stream starts 2^127 values after the one before it.

```csharp
using Mrg32k3a.NET;

var factory = new RandomStreamFactory();
RandomStream stream = factory.CreateStream("demand");
```

To use a different seed, pass an <xref:Mrg32k3a.NET.Mrg32k3aState>:

```csharp
var seed = new Mrg32k3aState(1, 2, 3, 4, 5, 6);
var factory = new RandomStreamFactory(seed);
```

A seed must follow the MRG32k3a rules. The first three values must be below 4294967087, the last
three below 4294944443, and neither group of three may be all zero. The constructor throws
`ArgumentException` if a rule is broken. If you'd rather check without an exception, use
<xref:Mrg32k3a.NET.Mrg32k3aState.TryCreate*>.

## Draw values

```csharp
double u = stream.NextDouble();                // strictly between 0 and 1
double fine = stream.NextDoubleHighPrecision(); // about 53 bits, uses two steps

int die = stream.NextInt32Inclusive(1, 6);      // closed range [1, 6]
int index = stream.Next(10);                    // half-open range [0, 10), like System.Random
long big = stream.NextInt64(0, 1_000_000_000_000);

var buffer = new double[1024];
stream.NextDoubles(buffer);                     // fill in bulk
```

Methods named `...Inclusive` include the upper bound. `Next` and `NextInt64` exclude it, as
`System.Random` does.

## Precision and antithetic variates

- <xref:Mrg32k3a.NET.RandomStream.HighPrecision> makes `NextDouble` return about 53 bits instead of
  32. Each draw then uses two steps of the generator, so set this flag before you start drawing,
  not partway through.
- <xref:Mrg32k3a.NET.RandomStream.Antithetic> makes the stream return `1 - u` instead of `u`. This
  does not change how far each draw advances the state, so a stream and its antithetic twin stay in
  sync.

## Threading

A factory can hand out streams to several threads at once. The streams themselves are **not** thread
safe, so give each worker its own stream.
