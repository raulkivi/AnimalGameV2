\ persist.fs — synthesize / EVALUATE / append / replay
\
\ There is no save-the-whole-tree operation here. Instead, `learn`
\ (tree.fs) calls the PERSIST-* words below
\ once per new fact; each one both EVALUATEs a freshly generated line of
\ Forth source against the live dictionary and appends the same text to
\ RULES-PATH — one generation step, two sinks. Loading is `load-words`,
\ which just interprets RULES-PATH; the Forth reader is the loader, there
\ is no custom parser.

REQUIRE node.fs

DECIMAL

\ RULES-PATH is a word, not a 2CONSTANT, so tests can redirect it to a
\ scratch file with SET-RULES-PATH instead of writing through the real
\ data/rules.fs.
256 CONSTANT RULES-PATH-BUFSIZE
CREATE rules-path-buf RULES-PATH-BUFSIZE ALLOT
: RULES-PATH ( -- c-addr u ) rules-path-buf COUNT ;
: set-rules-path ( c-addr u -- ) rules-path-buf PLACE ;
s" data/rules.fs" set-rules-path

\ ---------------------------------------------------------------------------
\ Line-buffer text synthesis
\ ---------------------------------------------------------------------------

CREATE line-buf 512 ALLOT

: line-reset ( -- ) 0 line-buf C! ;

: >ch ( char -- )
  line-buf COUNT + C!
  line-buf DUP C@ 1+ SWAP C!
;

: >str ( c-addr u -- ) line-buf +PLACE ;

\ >qstr  ( c-addr u -- )  appends  S" <text>"  as a literal Forth string,
\ built character-by-character so the payload never has to pass through
\ Forth's own S" parser (which would stop at the first embedded quote).
: >qstr ( c-addr u -- )
  [CHAR] S >ch  34 >ch  BL >ch
  >str
  34 >ch
;

: >sp ( -- ) BL >ch ;

: num>str ( n -- addr len ) 0 <# #S #> ;
: >num ( n -- ) num>str >str ;

\ >node-name  ( n -- )  appends the defining-name token  NODE-<n>
: >node-name ( n -- ) s" NODE-" >str >num ;

\ >tickname  ( n -- )  appends  ' NODE-<n>  — a reference to an
\ already-defined node's xt, for QUESTION-NODE/PATCH-*/GAME-ROOT-CELL args
: >tickname ( n -- )
  [CHAR] ' >ch >sp
  >node-name >sp
;

\ ---------------------------------------------------------------------------
\ Injection guard
\ ---------------------------------------------------------------------------
\ Player text becomes the payload of a >qstr-built S" ... " literal. A `"`
\ character inside it would close that literal early in every future
\ replay of RULES-PATH, letting the remainder of the line parse as
\ arbitrary Forth. This is the one load-bearing safety control in the
\ whole design — everything else about "data becomes code" is safe only
\ because word *names* are always synthesized (NODE-<n>), never derived
\ from player text.

: contains-quote? ( c-addr u -- flag )
  0 ?DO
    DUP I + C@ [CHAR] " = IF
      UNLOOP DROP TRUE EXIT
    THEN
  LOOP
  DROP FALSE
;

\ ---------------------------------------------------------------------------
\ File I/O
\ ---------------------------------------------------------------------------

\ file-nonempty?  ( c-addr u -- flag )  a 0-byte file is treated the same
\ as a missing one — both fall back to SEED-DEFAULT, since replaying an
\ empty file "succeeds" without ever binding GAME-ROOT-CELL.
: file-nonempty? ( c-addr u -- flag )
  2DUP R/O OPEN-FILE
  IF
    2DROP FALSE
  ELSE
    DUP >R
    FILE-SIZE THROW OR 0<>               \ nonzero size (either cell of the ud)
    R> CLOSE-FILE THROW
  THEN
;

: open-for-append ( c-addr u -- fileid )
  2DUP R/W OPEN-FILE
  IF
    DROP R/W CREATE-FILE THROW
  ELSE
    NIP NIP
  THEN
;

: append-line ( c-addr u -- )
  RULES-PATH open-for-append
  DUP >R
  FILE-SIZE THROW
  R@ REPOSITION-FILE THROW
  R@ WRITE-LINE THROW
  R> CLOSE-FILE THROW
;

\ commit-line  ( -- )  EVALUATE the current line-buf contents against the
\ live dictionary, then append the same text to RULES-PATH.
: commit-line ( -- )
  line-buf COUNT 2DUP EVALUATE
  append-line
;

\ ---------------------------------------------------------------------------
\ Public API — called from tree.fs's `learn`
\ ---------------------------------------------------------------------------

\ persist-new-animal  ( c-addr u -- xt )
\ Synthesizes, evaluates, and appends an ANIMAL-NODE definition; returns
\ the new leaf's xt.
: persist-new-animal ( c-addr u -- xt )
  predicted-num >R
  line-reset
  >qstr >sp  s" ANIMAL-NODE" >str >sp
  R@ >node-name
  commit-line
  R> xt-of
;

\ persist-new-question  ( yes-xt no-xt c-addr u -- xt )
\ Synthesizes, evaluates, and appends a QUESTION-NODE definition; returns
\ the new interior node's xt.
VARIABLE pq-yes-xt
VARIABLE pq-no-xt

: persist-new-question ( yes-xt no-xt c-addr u -- xt )
  2SWAP                                   \ ( c-addr u yes-xt no-xt )
  pq-no-xt !
  pq-yes-xt !                             \ ( c-addr u )
  predicted-num >R
  line-reset
  pq-yes-xt @ node-num >tickname
  pq-no-xt  @ node-num >tickname
  >qstr >sp
  s" QUESTION-NODE" >str >sp
  R@ >node-name
  commit-line
  R> xt-of
;

\ persist-set-root  ( xt -- )
: persist-set-root ( xt -- )
  node-num
  line-reset
  DUP >tickname s" GAME-ROOT-CELL !" >str
  DROP
  commit-line
;

\ persist-patch-yes  ( new-child-xt parent-xt -- )
: persist-patch-yes ( new-child-xt parent-xt -- )
  node-num SWAP node-num
  line-reset
  >tickname >tickname s" PATCH-YES" >str
  commit-line
;

\ persist-patch-no  ( new-child-xt parent-xt -- )
: persist-patch-no ( new-child-xt parent-xt -- )
  node-num SWAP node-num
  line-reset
  >tickname >tickname s" PATCH-NO" >str
  commit-line
;

\ ---------------------------------------------------------------------------
\ Bootstrap and load
\ ---------------------------------------------------------------------------

: seed-default ( -- )
  s" Dog" persist-new-animal persist-set-root
;

\ replay-rules reads RULES-PATH directly (OPEN-FILE/READ-LINE/EVALUATE)
\ rather than using INCLUDED: gforth's INCLUDED resolves a relative path
\ through its own file-search-path machinery, not the process's working
\ directory, so it can silently fail to find the exact same relative path
\ that OPEN-FILE (used everywhere else in this file) finds without issue.
\ Reading it ourselves keeps path resolution consistent everywhere.
512 CONSTANT REPLAY-BUFSIZE
CREATE replay-buf REPLAY-BUFSIZE ALLOT

: replay-rules ( -- )
  RULES-PATH R/O OPEN-FILE THROW >R
  BEGIN
    replay-buf REPLAY-BUFSIZE R@ READ-LINE THROW
  WHILE
    replay-buf SWAP EVALUATE
  REPEAT
  DROP
  R> CLOSE-FILE THROW
;

: load-words ( -- )
  RULES-PATH file-nonempty?
  IF
    ['] replay-rules CATCH IF
      seed-default
    THEN
  ELSE
    seed-default
  THEN
;
