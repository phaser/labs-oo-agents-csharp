# Porting: hypotheses

## REPL memory growth is more than linear (T15)

500 cells measured once on one machine: 19, 21, 28, 28, 31 MiB per 100 cells. Each new submission compiles against the chain of earlier submissions. Test with 2000 cells and with larger cells before choosing a cell cap (T17 or T22). Confirmed 0 times.

## TOCTOU on trace file symlink check (T19)

`JsonlSpanExporter` checks `LinkTarget` before it opens the file. A writer on the trace directory can swap in a link between the check and the open. The directory is created 0700, so only the owner can do this. Test on Linux with `O_NOFOLLOW` through a `SafeFileHandle` P/Invoke if the directory can be shared. Confirmed 0 times.

## One engine per call may be too costly (T17)

CodeAct builds a Roslyn engine, a validator and metadata references for every agentic call. The first cell of a call pays the script-host warm-up (about 0.3 to 0.5 s in the tests). Measure before you cache references across calls. Confirmed 0 times.
