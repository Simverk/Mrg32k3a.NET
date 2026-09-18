# Upgrading

Each section below covers one release boundary: what breaks, and what a snapshot saved by the older
version needs before the newer one loads it. Work forwards from the version you are on.

Everything that changed in a release, breaking or not, is listed in the
[changelog](https://github.com/Simverk/Mrg32k3a.NET/blob/main/CHANGELOG.md).

## From 0.1.0

Stream snapshots moved to version 2 to carry the substream index. <xref:Mrg32k3a.NET.RandomStream.LoadState*> and
<xref:Mrg32k3a.NET.RandomStream.FromState*> refuse a version 1 snapshot rather than loading it with
a substituted index, and throw `ArgumentException`.

Nothing else changed: the values a stream produces are unmoved.

### Migrating a saved snapshot

Version 2 adds that one property and changes nothing else, so a saved version 1 document can be
migrated by hand. Set `Version` to 2 and add `SubstreamIndex`, the substream the stream was on:

```json
{
  "Version": 2,
  "Name": "arrivals",
  "StreamStart": [ ... ],
  "SubstreamStart": [ ... ],
  "Current": [ ... ],
  "SubstreamIndex": 7,
  "Antithetic": false,
  "HighPrecision": false
}
```

The three state vectors carry over untouched, so the restored stream resumes at exactly the position
it was saved at.

The index has to be the substream that `SubstreamStart` actually begins. Version 2 checks the two
against each other and refuses the snapshot if they disagree.

### Where the index comes from

Nothing in a version 1 document carries it, and 0.1.0 has no `SubstreamIndex` property to read it
from. `SkipToNextSubstream` is that release's only way to move the marker, so the number is the
run's own count of those calls since the stream was created. Record that count alongside each
snapshot while still on 0.1.0, because 0.1.0 writes version 1 documents and cannot produce a
version 2 one.

Without the count the position is lost: the seed and stream index reach only the start of a stream,
not a position inside it. The stream can still be restarted at the beginning of its own block, by
setting `SubstreamIndex` to 0 and copying `StreamStart` into both `SubstreamStart` and `Current`.
The name and the two flags carry over, and the values run from the top of the stream again.

### While you are there

Factory snapshots are new in the same release. See
[Saving and restoring a factory](reproducibility.md#saving-and-restoring-a-factory).
