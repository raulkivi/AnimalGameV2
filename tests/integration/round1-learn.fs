\ round1-learn.fs — integration test, process 1
\
\ Simulates a fresh install: seeds the default tree, plays one round with
\ a scripted wrong guess, and lets `learn` write data/rules.fs-equivalent
\ (redirected to a scratch path) for real, through the same code path
\ main.fs uses. round2-verify-restart.fs then reads it back in a
\ genuinely separate gforth process — this is the only place the actual
\ cold-start replay guarantee ("V2 remembers what it learned last
\ session") gets exercised end-to-end.

REQUIRE test/tester.fs
REQUIRE ../../src/tree.fs

DECIMAL

s" /tmp/animalgamev2-integration-rules.fs" 2CONSTANT INTEGRATION-RULES-PATH
INTEGRATION-RULES-PATH W/O CREATE-FILE THROW CLOSE-FILE THROW   \ always start blank
INTEGRATION-RULES-PATH set-rules-path

20 CONSTANT MAX-ANSWERS
CREATE yn-answers MAX-ANSWERS ALLOT
VARIABLE yn-wr  VARIABLE yn-rd
: reset-yn ( -- ) 0 yn-wr ! 0 yn-rd ! ;
: push-yn ( flag -- ) yn-answers yn-wr @ + C!  yn-wr @ 1+ yn-wr ! ;
: scripted-ask-yesno ( c-addr u -- flag )
  2DROP yn-answers yn-rd @ + C@ 0<>  yn-rd @ 1+ yn-rd !
;

CREATE str-answers MAX-ANSWERS CELLS ALLOT
CREATE str-lens    MAX-ANSWERS CELLS ALLOT
VARIABLE str-wr  VARIABLE str-rd
: push-str ( c-addr u -- )
  str-wr @ CELLS str-lens    + !
  str-wr @ CELLS str-answers + !
  str-wr @ 1+ str-wr !
;
: scripted-prompt-line ( c-addr u -- c-addr2 u2 )
  2DROP
  str-rd @ CELLS str-answers + @
  str-rd @ CELLS str-lens    + @
  str-rd @ 1+ str-rd !
;
: scripted-display ( c-addr u -- ) 2DROP ;

' scripted-ask-yesno   IS ASK-YESNO
' scripted-prompt-line IS PROMPT-LINE
' scripted-display     IS DISPLAY

\ init-game equivalent: file is blank -> seed-default -> "Dog" is root
load-words
T{ GAME-ROOT-CELL @ node-leaf? -> TRUE }T

\ one round: wrong guess on the seeded "Dog", teach "Wolf"
reset-yn
FALSE push-yn                            \ "Is it a Dog?" -> NO
s" Wolf" push-str
s" Does it live in the wild?" push-str
TRUE push-yn                             \ Wolf answers YES

T{ GAME-ROOT-CELL traverse -> FALSE }T   \ round ends on the wrong guess

CR .( round1-learn.fs: seeded + learned Wolf, rules file written ) CR
