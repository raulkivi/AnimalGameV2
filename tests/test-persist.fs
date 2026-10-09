\ test-persist.fs — Unit tests for src/persist.fs
\
\ Redirects RULES-PATH to a scratch file so tests never touch the real
\ data/rules.fs. Full cold-start replay (write in one process, load in a
\ fresh one) is covered by the integration test, not here — within a
\ single process, node.fs's node-counter is shared across every test in
\ this file, so NODE-<n> names are only meaningful relative to *this*
\ process's own creation order.

REQUIRE test/tester.fs
REQUIRE ../src/persist.fs
REQUIRE tmp-path.fs

DECIMAL

s" test-rules.fs" tmp-path 2CONSTANT TEST-RULES-PATH
TEST-RULES-PATH R/W CREATE-FILE THROW CLOSE-FILE THROW   \ start from empty
TEST-RULES-PATH set-rules-path

\ ---------------------------------------------------------------------------
\ contains-quote?
\ ---------------------------------------------------------------------------

T{ s" plain text" contains-quote? -> FALSE }T
T{ s" "            contains-quote? -> FALSE }T

line-reset  s" has a " >str  34 >ch  s" quote here" >str
T{ line$ contains-quote? -> TRUE }T

\ ---------------------------------------------------------------------------
\ persist-new-animal / persist-new-question / persist-set-root
\ ---------------------------------------------------------------------------

s" Wolf"   persist-new-animal CONSTANT xt-wolf
s" Parrot" persist-new-animal CONSTANT xt-parrot

T{ xt-wolf   node-num -> 0 }T
T{ xt-parrot node-num -> 1 }T
T{ xt-wolf >BODY A-TEXT @  xt-wolf >BODY A-TLEN @  s" Wolf" COMPARE -> 0 }T

xt-wolf xt-parrot s" Is it a mammal?" persist-new-question CONSTANT xt-root
T{ xt-root node-num -> 2 }T
T{ xt-root >BODY Q-YES @ -> xt-wolf   }T
T{ xt-root >BODY Q-NO  @ -> xt-parrot }T

xt-root persist-set-root
T{ GAME-ROOT-CELL @ -> xt-root }T

\ ---------------------------------------------------------------------------
\ persist-patch-yes / persist-patch-no
\ ---------------------------------------------------------------------------

s" Lizard" persist-new-animal CONSTANT xt-lizard
xt-lizard xt-root persist-patch-yes
T{ xt-root >BODY Q-YES @ -> xt-lizard }T
T{ xt-root >BODY Q-NO  @ -> xt-parrot }T   \ untouched

s" Cat" persist-new-animal CONSTANT xt-cat
xt-cat xt-root persist-patch-no
T{ xt-root >BODY Q-NO @ -> xt-cat     }T
T{ xt-root >BODY Q-YES @ -> xt-lizard }T   \ untouched

\ ---------------------------------------------------------------------------
\ Every commit-line both evaluates AND appends: the file should now
\ contain a line for each operation above.
\ ---------------------------------------------------------------------------

VARIABLE scan-fileid
VARIABLE scan-found
VARIABLE scan-needle-addr
VARIABLE scan-needle-len

: scan-file-for ( needle-addr needle-u -- flag )
  scan-needle-len ! scan-needle-addr !
  FALSE scan-found !
  RULES-PATH R/O OPEN-FILE THROW scan-fileid !
  BEGIN
    PAD 256 scan-fileid @ READ-LINE THROW
  WHILE
    PAD SWAP scan-needle-addr @ scan-needle-len @ SEARCH NIP NIP
    IF TRUE scan-found ! THEN
  REPEAT
  DROP
  scan-fileid @ CLOSE-FILE THROW
  scan-found @
;

T{ s" ANIMAL-NODE"    scan-file-for -> TRUE }T
T{ s" QUESTION-NODE"  scan-file-for -> TRUE }T
T{ s" PATCH-YES"      scan-file-for -> TRUE }T
T{ s" PATCH-NO"       scan-file-for -> TRUE }T
T{ s" GAME-ROOT-CELL" scan-file-for -> TRUE }T

\ ---------------------------------------------------------------------------
\ load-words fallback paths (do not depend on prior NODE-* numbering)
\ ---------------------------------------------------------------------------

s" test-missing.fs" tmp-path 2CONSTANT MISSING-PATH
MISSING-PATH set-rules-path
load-words
T{ GAME-ROOT-CELL @ node-num -> predicted-num 1- }T   \ a fresh leaf was seeded
T{ GAME-ROOT-CELL @ node-leaf? -> TRUE }T

s" test-corrupt.fs" tmp-path 2CONSTANT CORRUPT-PATH

: write-corrupt ( -- )
  CORRUPT-PATH R/W CREATE-FILE THROW >R
  s" THIS-WORD-DOES-NOT-EXIST" R@ WRITE-LINE THROW
  R> CLOSE-FILE THROW
;
write-corrupt

CORRUPT-PATH set-rules-path
load-words
T{ GAME-ROOT-CELL @ node-leaf? -> TRUE }T

\ ---------------------------------------------------------------------------
\ Missing parent directory (fresh clone: data/ is not there yet) — the
\ first append must create it rather than THROW out of CREATE-FILE.
\ ---------------------------------------------------------------------------

s" no-such-dir/nested/rules.fs" tmp-path 2CONSTANT NODIR-PATH
NODIR-PATH set-rules-path
load-words
T{ GAME-ROOT-CELL @ node-leaf? -> TRUE }T
T{ NODIR-PATH file-nonempty? -> TRUE }T

\ ---------------------------------------------------------------------------
\ Long text round-trips. line-buf used to be a counted string whose length
\ byte wrapped at 255 even though 512 bytes were allotted, so a 250-char
\ name was written (and EVALUATEd) as a garbled `xxxx NODE-<n>` line.
\ ---------------------------------------------------------------------------

s" test-long.fs" tmp-path 2CONSTANT LONG-PATH
LONG-PATH set-rules-path

CREATE long-text 256 ALLOT
long-text 256 CHAR x FILL

\ last-line  ( -- c-addr u )  the final line of RULES-PATH, copied to PAD
CREATE last-line-buf 1024 ALLOT
VARIABLE last-line-len
: last-line ( -- c-addr u )
  0 last-line-len !
  RULES-PATH R/O OPEN-FILE THROW scan-fileid !
  BEGIN
    PAD 1024 scan-fileid @ READ-LINE THROW
  WHILE
    DUP last-line-len !  PAD last-line-buf ROT MOVE
  REPEAT
  DROP
  scan-fileid @ CLOSE-FILE THROW
  last-line-buf last-line-len @
;

\ expected-line  builds the line we expect, independently of line-buf
CREATE expect-buf 1024 ALLOT
VARIABLE expect-len
: expect-reset ( -- ) 0 expect-len ! ;
: expect+ ( c-addr u -- ) TUCK expect-buf expect-len @ + SWAP MOVE  expect-len +! ;
: expect$ ( -- c-addr u ) expect-buf expect-len @ ;

predicted-num CONSTANT long-num
long-text 250 persist-new-animal CONSTANT xt-long
T{ xt-long >BODY A-TLEN @ -> 250 }T
T{ xt-long >BODY a-text$ long-text 250 COMPARE -> 0 }T
expect-reset  s\" S\" " expect+  long-text 250 expect+
s\" \" ANIMAL-NODE NODE-" expect+  long-num num>str expect+
T{ last-line expect$ COMPARE -> 0 }T

\ the longest text the UI can accept (UI-BUFSIZE) as a question
predicted-num CONSTANT longq-num
xt-long xt-wolf long-text UI-BUFSIZE persist-new-question CONSTANT xt-longq
T{ xt-longq >BODY Q-TLEN @ -> UI-BUFSIZE }T
T{ xt-longq >BODY q-text$ long-text UI-BUFSIZE COMPARE -> 0 }T
T{ last-line NIP UI-BUFSIZE > -> TRUE }T

\ ---------------------------------------------------------------------------
\ Corrupt rules file: moved aside, then seeded fresh exactly once.
\ Previously load-words appended a new Dog seed *after* the bad line, so
\ every later launch failed at the same line and appended yet another seed.
\ ---------------------------------------------------------------------------

: count-lines-with ( needle-addr needle-u -- n )
  scan-needle-len ! scan-needle-addr !
  0 scan-found !
  RULES-PATH R/O OPEN-FILE THROW scan-fileid !
  BEGIN
    PAD 256 scan-fileid @ READ-LINE THROW
  WHILE
    PAD SWAP scan-needle-addr @ scan-needle-len @ SEARCH NIP NIP
    IF 1 scan-found +! THEN
  REPEAT
  DROP
  scan-fileid @ CLOSE-FILE THROW
  scan-found @
;

: path-exists? ( c-addr u -- flag ) FILE-STATUS NIP 0= ;

s" test-corrupt2.fs" tmp-path 2CONSTANT CORRUPT2-PATH
s" test-corrupt2.fs.corrupt-1" tmp-path 2CONSTANT CORRUPT2-ASIDE-1
s" test-corrupt2.fs.corrupt-2" tmp-path 2CONSTANT CORRUPT2-ASIDE-2

: write-corrupt2 ( -- )   \ one valid line, then garbage
  CORRUPT2-PATH R/W CREATE-FILE THROW >R
  s\" S\" Cat\" ANIMAL-NODE NODE-900" R@ WRITE-LINE THROW
  s" xxxxxxx NODE-0" R@ WRITE-LINE THROW
  R> CLOSE-FILE THROW
;
write-corrupt2
CORRUPT2-PATH set-rules-path

predicted-num CONSTANT before-load
load-words
T{ GAME-ROOT-CELL @ node-leaf? -> TRUE }T
T{ GAME-ROOT-CELL @ >BODY a-text$ s" Dog" COMPARE -> 0 }T
\ the node counter is rolled back, so the fresh file's NODE-<n> names match
\ the nums a fresh process will assign when it replays it
T{ GAME-ROOT-CELL @ node-num -> before-load }T
T{ CORRUPT2-ASIDE-1 path-exists? -> TRUE }T
T{ s" ANIMAL-NODE" count-lines-with -> 1 }T
T{ s" xxxxxxx" count-lines-with -> 0 }T
\ the corrupt content is preserved in the moved-aside copy
CORRUPT2-ASIDE-1 set-rules-path
T{ s" xxxxxxx" count-lines-with -> 1 }T
CORRUPT2-PATH set-rules-path

\ a second launch replays the fresh file cleanly: no new seed, no new
\ moved-aside copy
load-words
T{ s" ANIMAL-NODE" count-lines-with -> 1 }T
T{ CORRUPT2-ASIDE-2 path-exists? -> FALSE }T
T{ GAME-ROOT-CELL @ >BODY a-text$ s" Dog" COMPARE -> 0 }T

\ corrupting it again picks the next free .corrupt-<n> name
write-corrupt2
load-words
T{ CORRUPT2-ASIDE-2 path-exists? -> TRUE }T
T{ s" ANIMAL-NODE" count-lines-with -> 1 }T

CR .( test-persist.fs: all tests passed ) CR
