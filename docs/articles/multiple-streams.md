# Multiple streams

MRG32k3a has a period of about 2^191. This library divides that sequence in two levels:

| Level     | Size    | How to get the next one            | How to get the one at index _k_        |
|-----------|---------|------------------------------------|----------------------------------------|
| Stream    | 2^127   | `RandomStreamFactory.CreateStream` | `RandomStreamFactory.CreateStreamAt`   |
| Substream | 2^76    | `RandomStream.SkipToNextSubstream` | `RandomStream.SkipToSubstream`         |

Streams from the same factory never overlap in any realistic run.

## One stream per source of randomness

A simulation model usually needs one stream per source of randomness. Give arrivals, service times,
routing decisions and so on each their own stream. Then a change in how one source is used does
not shift the numbers the other sources see.

```csharp
var factory = new RandomStreamFactory();
var arrivals = factory.CreateStream("arrivals");
var service = factory.CreateStream("service");
```

## Substreams for replications

Use one substream per replication. After each replication, move every stream to its next
substream:

```csharp
for (var replication = 0; replication < 100; replication++)
{
    RunModel(arrivals, service);

    arrivals.SkipToNextSubstream();
    service.SkipToNextSubstream();
}
```

## Substreams by index

A stream holds 2^51 substreams, numbered from zero.
<xref:Mrg32k3a.NET.RandomStream.SkipToSubstream*> goes straight to one of them, counting from the
start of the stream rather than from wherever the stream happens to be:

```csharp
// Resume a batch at replication 500 without replaying the first 500.
arrivals.SkipToSubstream(500);
service.SkipToSubstream(500);
```

<xref:Mrg32k3a.NET.RandomStream.SkipSubstreams*> is the relative form, and takes negative counts to
move back towards the start of the stream:

```csharp
arrivals.SkipSubstreams(10);   // ten replications on
arrivals.SkipSubstreams(-3);   // three back, to re-run them
```

Both cost about the same as a single `SkipToNextSubstream` however far they move. Walking a thousand substreams with `SkipToNextSubstream` costs
a thousand jumps; `SkipToSubstream(1000)` costs one.

<xref:Mrg32k3a.NET.RandomStream.SubstreamIndex> reports where a stream currently is, and is carried
by `Clone` and by the state snapshot.

A stream never leaves its own block. An index at or beyond 2^51, or a count that would move before
substream zero, throws `ArgumentOutOfRangeException` rather than walking into the next stream's
values; `SkipToNextSubstream` likewise throws `InvalidOperationException` on the last substream.

## Common random numbers

When you compare two configurations, you usually care about the difference in their outputs, not
each output on its own. If each configuration gets its own independent random inputs, part of the
observed difference comes from those inputs rather than from the configurations. Feeding both the
same inputs removes that noise. The two results become positively correlated, and the variance of
their difference, `Var(A) + Var(B) - 2 Cov(A, B)`, shrinks. You can then detect a real difference
with fewer replications.

This works best when each input is used for the same purpose in both runs. That is why each source
of randomness should have its own stream. Otherwise a change in how one source is used shifts the
numbers every later draw sees, and the inputs stop matching.

To compare two system configurations with the same random inputs, rewind to the start of the
substream before running the second configuration:

```csharp
RunModel(configurationA, arrivals, service);

arrivals.RewindSubstream();
service.RewindSubstream();
RunModel(configurationB, arrivals, service);
```

<xref:Mrg32k3a.NET.RandomStream.RewindStream> goes back to the very start of the stream, at its first
substream.

## Parallel and distributed runs

`CreateStreams(count)` hands out several consecutive streams at once, for example one per worker
thread:

```csharp
RandomStream[] perWorker = factory.CreateStreams(Environment.ProcessorCount);
```

If each worker runs in a separate process and only knows its rank, it can build its own stream
directly with <xref:Mrg32k3a.NET.RandomStreamFactory.CreateStreamAt*>. Every process uses the same seed:

```csharp
var factory = new RandomStreamFactory(sharedSeed);
RandomStream mine = factory.CreateStreamAt(rank);
```

`CreateStreamAt` returns the same stream that `CreateStream` would return at that position. It does
not change the factory's creation order, and its cost grows with the logarithm of the index.

## Jumping within a stream

<xref:Mrg32k3a.NET.RandomStream.Advance*>, <xref:Mrg32k3a.NET.RandomStream.AdvanceByPowerOfTwo*> and
<xref:Mrg32k3a.NET.RandomStream.RetreatByPowerOfTwo*> move the current position by any number of
steps, forwards or backwards. They are escape hatches for special cases. Most code only needs
substreams and more streams from the factory.

Unlike the substream operations, these three do not refuse to leave the stream's own block. A
position inside a substream can be up to 2^76 steps from its start, which no `long` can express, so
there is no offset for them to check a move against. Enough steps will walk into a neighbouring
stream.
