\ tmp-path.fs — per-run scratch paths for tests
\
\ The Makefile creates a fresh directory with `mktemp -d` for every test
\ recipe and passes it in ANIMALGAMEV2_TMP, so concurrent or repeated runs
\ never share (or trip over) fixed /tmp/animalgamev2-* files. Run by hand
\ without the variable, paths fall back to /tmp.

DECIMAL

: test-tmp-dir ( -- c-addr u )
  s" ANIMALGAMEV2_TMP" getenv
  DUP 0= IF 2DROP s" /tmp" THEN
;

\ tmp-path  ( name-addr name-u -- path-addr path-u )  heap-allocates
\ "<test-tmp-dir>/<name>"; the result stays valid for the whole run.
: tmp-path ( name-addr name-u -- path-addr path-u )
  test-tmp-dir                         ( n-a n-u d-a d-u )
  2 PICK OVER + 1+ DUP ALLOCATE THROW  ( n-a n-u d-a d-u total buf )
  SWAP >R >R                           ( n-a n-u d-a d-u ) ( R: total buf )
  R@ SWAP DUP >R MOVE                  ( n-a n-u ) ( R: total buf d-u )
  [CHAR] / R> R@ + DUP >R C!           ( n-a n-u ) ( R: total buf slash-addr )
  R> 1+ SWAP MOVE
  R> R>
;
