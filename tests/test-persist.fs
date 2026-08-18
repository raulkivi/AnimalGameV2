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

DECIMAL

s" /tmp/animalgamev2-test-rules.fs" 2CONSTANT TEST-RULES-PATH
TEST-RULES-PATH R/W CREATE-FILE THROW CLOSE-FILE THROW   \ start from empty
TEST-RULES-PATH set-rules-path

\ ---------------------------------------------------------------------------
\ contains-quote?
\ ---------------------------------------------------------------------------

T{ s" plain text" contains-quote? -> FALSE }T
T{ s" "            contains-quote? -> FALSE }T

line-reset  s" has a " >str  34 >ch  s" quote here" >str
T{ line-buf COUNT contains-quote? -> TRUE }T

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

s" /tmp/animalgamev2-test-missing.fs" 2CONSTANT MISSING-PATH
MISSING-PATH set-rules-path
load-words
T{ GAME-ROOT-CELL @ node-num -> predicted-num 1- }T   \ a fresh leaf was seeded
T{ GAME-ROOT-CELL @ node-leaf? -> TRUE }T

s" /tmp/animalgamev2-test-corrupt.fs" 2CONSTANT CORRUPT-PATH

: write-corrupt ( -- )
  CORRUPT-PATH R/W CREATE-FILE THROW >R
  s" THIS-WORD-DOES-NOT-EXIST" R@ WRITE-LINE THROW
  R> CLOSE-FILE THROW
;
write-corrupt

CORRUPT-PATH set-rules-path
load-words
T{ GAME-ROOT-CELL @ node-leaf? -> TRUE }T

CR .( test-persist.fs: all tests passed ) CR
