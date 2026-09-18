# Changelog

All notable changes to Mrg32k3a.NET are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Migration steps for the breaking changes are in the
[upgrade guide](https://simverk.github.io/Mrg32k3a.NET/articles/upgrading.html).

## 0.2.0 - 2026-09-18

### Added

- `RandomStream.SkipSubstreams` and `RandomStream.SkipToSubstream` move the substream marker in
  time logarithmic in the distance, the first relative and signed so that it also moves back
  towards the stream start, the second absolute and counted from the stream start. Each is one
  modular matrix exponentiation per component rather than a run of table jumps, so a
  thousand-substream move costs about as much as a twenty-substream one.
- `RandomStream.SubstreamIndex` reports the substream the stream is inside. A relative jump needs
  that number to tell whether the move would leave the stream's own block of 2^51 substreams, and
  it cannot be recovered from the state vectors.
- `RandomStreamFactoryState`, `RandomStreamFactory.SaveState` and `RandomStreamFactory.FromState`
  persist the factory's seed and the number of streams it has handed out. A run that snapshotted
  only its streams came back with a factory positioned at its first stream, reissuing streams that
  were already live, so two workers drew the same values with nothing to report it. Restore is by
  construction: there is no counterpart that loads into an existing factory, because a factory is
  shared between threads and replacing its seed in place would let one worker take a stream from
  the old ordering while another takes one from the new.

### Fixed

- `LoadState` and `FromState` reject a snapshot whose `SubstreamStart` is not the start of
  substream `SubstreamIndex` of `StreamStart`. Each vector was previously checked against the seed
  rules alone and never against the others, so a snapshot naming substream zero while its
  `SubstreamStart` sat in another stream's block was accepted, and the substream guards that take
  the index as the stream's position would then authorise a jump out of the block they exist to
  protect.
- `SkipToNextSubstream` throws on the last substream of a stream rather than crossing into the next
  stream's block.
- The three entry points that take a `uint[]` seed, the `Mrg32k3aState` array constructor,
  `Mrg32k3aState.TryCreate` and `RandomStream.LoadState`, copy the array once and then validate and
  build from the copy alone. Each previously read the caller's array twice, so an array mutated
  between the two reads could have one seed accepted and a different one stored, including one the
  seed rules reject.

### Changed

- **Breaking.** `RandomStreamState` moves to version 2 to carry `SubstreamIndex`. A version 1
  snapshot is refused rather than loaded with a substituted index, because no such index can be
  derived from its vectors. The values a stream produces are unmoved. See
  [Upgrading: from 0.1.0](https://simverk.github.io/Mrg32k3a.NET/articles/upgrading.html#from-010).

## 0.1.0 - 2026-09-17

Initial release.
