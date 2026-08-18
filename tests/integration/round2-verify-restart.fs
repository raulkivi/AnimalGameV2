\ round2-verify-restart.fs — integration test, process 2 (genuinely fresh)
\
\ Loads the exact same module set as process 1, in a brand new gforth
\ invocation with an empty dictionary — then points RULES-PATH at the
\ file process 1 wrote and calls load-words, exercising the real
\ REQUIRE.../INCLUDED cold-start path rather than the live EVALUATE path.
\ If the animal learned in round 1 is reachable here, persistence across
\ restarts genuinely works, not just within one process.

REQUIRE test/tester.fs
REQUIRE ../../src/tree.fs

DECIMAL

s" /tmp/animalgamev2-integration-rules.fs" 2CONSTANT INTEGRATION-RULES-PATH
INTEGRATION-RULES-PATH set-rules-path

20 CONSTANT MAX-ANSWERS
CREATE yn-answers MAX-ANSWERS ALLOT
VARIABLE yn-wr  VARIABLE yn-rd
: reset-yn ( -- ) 0 yn-wr ! 0 yn-rd ! ;
: push-yn ( flag -- ) yn-answers yn-wr @ + C!  yn-wr @ 1+ yn-wr ! ;
: scripted-ask-yesno ( c-addr u -- flag )
  2DROP yn-answers yn-rd @ + C@ 0<>  yn-rd @ 1+ yn-rd !
;
: scripted-display ( c-addr u -- ) 2DROP ;
' scripted-ask-yesno IS ASK-YESNO
' scripted-display    IS DISPLAY

load-words
T{ GAME-ROOT-CELL @ node-leaf? -> FALSE }T   \ root is now a question, not the seed leaf

reset-yn
TRUE push-yn      \ "does it live in the wild?" -> YES -> Wolf
TRUE push-yn      \ "Is it a Wolf?" -> YES
T{ GAME-ROOT-CELL traverse -> TRUE }T

reset-yn
FALSE push-yn     \ "does it live in the wild?" -> NO -> Dog
TRUE push-yn      \ "Is it a Dog?" -> YES
T{ GAME-ROOT-CELL traverse -> TRUE }T

CR .( round2-verify-restart.fs: cold-start replay reached Wolf and Dog ) CR
