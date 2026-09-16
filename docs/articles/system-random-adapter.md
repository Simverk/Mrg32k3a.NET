# Using a stream as System.Random

Some APIs expect a `System.Random`. <xref:Mrg32k3a.NET.RandomStream.AsRandom*> wraps a stream in a
<xref:Mrg32k3a.NET.StreamBackedRandom>, which inherits from `Random`:

```csharp
var factory = new RandomStreamFactory();
RandomStream stream = factory.CreateStream("shuffle");
Random random = stream.AsRandom();

var items = new[] { 1, 2, 3, 4, 5 };
random.Shuffle(items); // .NET 8+
```

The adapter doesn't copy the stream. It draws from the stream it wraps, so draws through the adapter
and draws taken directly from the stream advance the same state. You can reach the stream again
through <xref:Mrg32k3a.NET.StreamBackedRandom.Stream>.

Every `Random` member draws from the stream, including `NextBytes`, `NextInt64` and `NextSingle` on
.NET 8 and later. The results are therefore as reproducible as the stream itself. Like the stream,
the adapter is not thread safe.
