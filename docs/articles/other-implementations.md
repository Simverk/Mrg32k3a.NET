# Implementations in other languages

MRG32k3a and its stream and substream scheme have been implemented in many languages and
environments. The list below is a starting point if you need the same generator outside .NET.

Mrg32k3a.NET reproduces the output of the RngStreams C reference implementation bit for bit, and
its test suite checks this. The other implementations listed below have not been compared against
it.

## Reference implementations

- **C, C++ and Java.** Pierre L'Ecuyer's original `RngStreams` code, from the authors of the
  stream and substream paper, is available from
  [L'Ecuyer's software page](https://www-labs.iro.umontreal.ca/~lecuyer/myftp/streams00/).
- **C (GNU-style package).** [RngStreams](https://statmath.wu.ac.at/software/RngStreams/)
  packages the C version as a library, maintained by Josef Leydold.

## Other languages and environments

- **Java.** [SSJ](https://github.com/umontreal-simul/ssj), the Stochastic Simulation in Java
  library from L'Ecuyer's group, includes an `MRG32k3a` class.
- **Python.** [mrg32k3a](https://github.com/simopt-admin/mrg32k3a)
  ([PyPI](https://pypi.org/project/mrg32k3a/)), from the SimOpt project, supports streams,
  substreams and subsubstreams.
- **R.** The [rstream](https://cran.r-project.org/package=rstream) package provides
  `rstream.mrg32k3a`. Base R's `parallel` package also uses the generator through
  `RNGkind("L'Ecuyer-CMRG")`; see the
  [parallel package vignette](https://stat.ethz.ch/R-manual/R-devel/library/parallel/doc/parallel.pdf).
- **MATLAB.** [`RandStream`](https://www.mathworks.com/help/matlab/ref/randstream.html) supports
  the `mrg32k3a` generator type with multiple streams and substreams.
