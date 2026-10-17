# Porting: hypotheses

## REPL memory growth is more than linear (T15)

500 cells measured once on one machine: 19, 21, 28, 28, 31 MiB per 100 cells. Each new submission compiles against the chain of earlier submissions. Test with 2000 cells and with larger cells before choosing a cell cap (T17 or T22). Confirmed 0 times.
