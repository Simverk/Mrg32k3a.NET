# Reproducibility and state

## Same seed, same numbers, everywhere

The recurrence runs on 64-bit integer arithmetic with no floating-point steps. Given a seed, the
output is bit-for-bit identical on every supported target framework and on both x64 and ARM64.
Two factories built from the same seed hand out the same streams in the same order, and they don't
affect each other.

Treat the seed as part of your experiment's configuration. Record it with your results and use it
again to reproduce a run:

```csharp
var seed = new Mrg32k3aState(12345, 12345, 12345, 12345, 12345, 12345);
var factory = new RandomStreamFactory(seed);
```

## Saving and restoring a stream

<xref:Mrg32k3a.NET.RandomStream.SaveState*> takes a <xref:Mrg32k3a.NET.RandomStreamState> snapshot.
The snapshot holds the stream start, the substream start, the current position, the index of that
substream, the name, and both output flags. Use it to checkpoint a long run or to pass a stream to
another process.

```csharp
RandomStreamState snapshot = stream.SaveState();

// ... later, or in another process
RandomStream resumed = RandomStream.FromState(snapshot);

// or restore an existing instance in place
stream.LoadState(snapshot);
```

`RandomStreamState` is a plain object with public properties and no serializer dependency, so any
serializer can handle it:

```csharp
using System.Text.Json;

string json = JsonSerializer.Serialize(stream.SaveState());
RandomStream restored = RandomStream.FromState(
    JsonSerializer.Deserialize<RandomStreamState>(json)!);
```

The snapshot has a `Version` field. `LoadState` and `FromState` throw `ArgumentException` for any
version other than the current one, or for a state that breaks the seed rules.

The current version is 2.

## Saving and restoring a factory

A stream snapshot records where a stream is. It does not record which streams have been handed out,
and that lives on the factory. Save both, or a reloaded run starts its factory at the first stream
again and hands out streams that are already in use, so two workers draw the same values with
nothing to report it.

<xref:Mrg32k3a.NET.RandomStreamFactory.SaveState*> takes a
<xref:Mrg32k3a.NET.RandomStreamFactoryState>, holding the seed and the number of streams handed out
so far:

```csharp
var checkpoint = new
{
    Factory = factory.SaveState(),
    Arrivals = arrivals.SaveState(),
    Service = service.SaveState(),
};
```

<xref:Mrg32k3a.NET.RandomStreamFactory.FromState*> builds a factory whose next stream is the one the
saved factory would have handed out next:

```csharp
RandomStreamFactory factory = RandomStreamFactory.FromState(checkpoint.Factory);
RandomStream arrivals = RandomStream.FromState(checkpoint.Arrivals);
RandomStream service = RandomStream.FromState(checkpoint.Service);

// Carries on from stream 3, not from stream 0.
RandomStream routing = factory.CreateStream("routing");
```

There is no counterpart that loads into an existing factory. A factory is shared between threads, so
replacing its seed underneath them would let one worker take a stream from the old ordering and
another from the new one, which is the duplication the snapshot exists to prevent. Build a new
factory and replace your reference to the old one.

## Cloning

<xref:Mrg32k3a.NET.RandomStream.Clone*> makes an independent copy at the current position. The copy
produces exactly the same values from then on. This is useful when you want to try something
without using up the original stream.

## Inspecting state

<xref:Mrg32k3a.NET.RandomStream.CurrentState>, <xref:Mrg32k3a.NET.RandomStream.StreamStartState> and
<xref:Mrg32k3a.NET.RandomStream.SubstreamStartState> expose the three state vectors, and
<xref:Mrg32k3a.NET.RandomStream.SubstreamIndex> the substream the stream is inside.
`ToDetailedString()` prints all of them, with the name and flags, for diagnostics.

A stream that has been restored can be put back on a known substream without replaying anything,
because the index is part of the snapshot:

```csharp
RandomStream resumed = RandomStream.FromState(snapshot);
resumed.SkipToSubstream(replication);
```
