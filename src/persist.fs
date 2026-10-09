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

\ line-buf keeps its length in its own cell (line-len), not a leading count
\ byte: a counted string's length silently wraps at 255, which once turned a
\ long animal name into a garbled, unreplayable `xxxx NODE-<n>` line. Any
\ line that would overflow the buffer aborts instead of being truncated.
\ The longest line generated from UI input (UI-BUFSIZE chars of text plus
\ two ' NODE-<n> references and the QUESTION-NODE word) fits comfortably.
512 CONSTANT LINE-BUFSIZE
CREATE line-buf LINE-BUFSIZE ALLOT
VARIABLE line-len

: line-reset ( -- ) 0 line-len ! ;
: line$ ( -- c-addr u ) line-buf line-len @ ;

: line-room ( u -- )
  line-len @ + LINE-BUFSIZE > ABORT" generated rules line too long"
;

: >str ( c-addr u -- )
  DUP line-room
  TUCK line$ + SWAP MOVE
  line-len +!
;

: >ch ( char -- ) SP@ 1 >str DROP ;

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
    NIP NIP DUP >R
    FILE-SIZE THROW OR 0<>               \ nonzero size (either cell of the ud)
    R> CLOSE-FILE THROW
  THEN
;

\ path-dir  ( c-addr u -- c-addr u' )  everything before the last `/`;
\ u' = 0 when the path has no directory part.
: path-dir ( c-addr u -- c-addr u' )
  BEGIN
    DUP 0> WHILE
    2DUP + 1- C@ [CHAR] / = IF 1- EXIT THEN
    1-
  REPEAT
;

\ ensure-parent-dir  ( c-addr u -- )  creates the rules file's directory
\ (and any missing parents) so a fresh clone with no data/ still works.
\ Errors are ignored here; the CREATE-FILE that follows reports them.
: ensure-parent-dir ( c-addr u -- )
  path-dir DUP IF $1FF mkdir-parents DROP ELSE 2DROP THEN
;

: create-failed ( ior -- )
  s" Cannot create the rules file " PAD PLACE
  RULES-PATH PAD +PLACE
  s"  -- run the game from the project root, with a writable data/ directory." PAD +PLACE
  PAD COUNT DISPLAY
  THROW
;

: open-for-append ( c-addr u -- fileid )
  2DUP R/W OPEN-FILE
  IF
    DROP
    2DUP ensure-parent-dir
    R/W CREATE-FILE ?DUP IF NIP create-failed THEN
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
  line$ 2DUP EVALUATE
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
\ The file is closed even when a line THROWs, and a replay that never binds
\ GAME-ROOT-CELL counts as a failure too (the game could not start from it).
LINE-BUFSIZE CONSTANT REPLAY-BUFSIZE
CREATE replay-buf REPLAY-BUFSIZE ALLOT
VARIABLE replay-fid

: replay-lines ( -- )
  BEGIN
    replay-buf REPLAY-BUFSIZE replay-fid @ READ-LINE THROW
  WHILE
    replay-buf SWAP EVALUATE
  REPEAT
  DROP
;

: replay-rules ( -- )
  RULES-PATH R/O OPEN-FILE THROW replay-fid !
  0 GAME-ROOT-CELL !
  ['] replay-lines CATCH
  replay-fid @ CLOSE-FILE DROP
  THROW
  GAME-ROOT-CELL @ 0= ABORT" rules file never sets GAME-ROOT-CELL"
;

\ ---------------------------------------------------------------------------
\ Corrupt-file recovery
\ ---------------------------------------------------------------------------
\ A rules file that fails to replay is renamed to <path>.corrupt-<n> (first
\ free n) before seeding, so the fresh seed starts a new, clean file. Seeding
\ by appending to the bad file instead would leave the bad line first, so
\ every later launch would fail at it again and append yet another seed.

CREATE aside-buf RULES-PATH-BUFSIZE 32 + ALLOT
VARIABLE aside-len

: aside-path ( n -- c-addr u )
  RULES-PATH TUCK aside-buf SWAP MOVE  aside-len !
  s" .corrupt-" TUCK aside-buf aside-len @ + SWAP MOVE  aside-len +!
  num>str TUCK aside-buf aside-len @ + SWAP MOVE  aside-len +!
  aside-buf aside-len @
;

: move-aside ( -- )
  1 BEGIN DUP aside-path FILE-STATUS NIP 0= WHILE 1+ REPEAT
  aside-path RULES-PATH 2SWAP RENAME-FILE
  ?DUP IF
    s" Could not move the unreadable rules file out of the way: " PAD PLACE
    RULES-PATH PAD +PLACE  PAD COUNT DISPLAY
    THROW
  THEN
  s" The rules file could not be loaded; it was moved to " PAD PLACE
  aside-buf aside-len @ PAD +PLACE
  s"  and the game starts fresh." PAD +PLACE
  PAD COUNT DISPLAY
;

: load-words ( -- )
  predicted-num >R
  RULES-PATH file-nonempty?
  IF
    ['] replay-rules CATCH IF
      R@ node-count !          \ fresh file's NODE-<n> names start where
      move-aside               \ a new process's replay will number them
      WARNINGS @  WARNINGS OFF \ re-seeding may redefine a partially
      seed-default             \ replayed NODE-<n>; that's expected
      WARNINGS !
    THEN
  ELSE
    seed-default
  THEN
  R> DROP
;
