\ test-node.fs — Unit tests for src/node.fs
\
\ Uses gforth's built-in tester.fs: T{ <words> -> <expected> }T

REQUIRE test/tester.fs
REQUIRE ../src/node.fs

DECIMAL

\ ---------------------------------------------------------------------------
\ Scripted ASK-YESNO / DISPLAY (node.fs's DOES> actions call these)
\ ---------------------------------------------------------------------------

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

\ Stub learn-hook so ANIMAL-NODE's wrong-guess path is isolated from
\ tree.fs/persist.fs, which this file does not (and must not) depend on.
VARIABLE learn-hook-called
: stub-learn-hook ( cell-addr old-xt parent-num branch-is-yes -- )
  2DROP 2DROP
  TRUE learn-hook-called !
;
' stub-learn-hook IS learn-hook

\ ---------------------------------------------------------------------------
\ ANIMAL-NODE tests
\ ---------------------------------------------------------------------------

S" Dog" ANIMAL-NODE NODE-0

T{ ' NODE-0 node-num -> 0 }T
T{ 0 xt-of         -> ' NODE-0 }T
T{ ' NODE-0 >BODY A-TEXT @  ' NODE-0 >BODY A-TLEN @  s" Dog" COMPARE -> 0 }T

VARIABLE t-cell
' NODE-0 t-cell !

reset-yn
TRUE push-yn                     \ "Is it a Dog?" -> YES (correct guess)
T{ t-cell -1 FALSE dispatch-node -> TRUE }T

FALSE learn-hook-called !
reset-yn
FALSE push-yn                    \ "Is it a Dog?" -> NO (wrong guess)
T{ t-cell -1 FALSE dispatch-node -> FALSE }T
T{ learn-hook-called @ -> TRUE }T

\ ---------------------------------------------------------------------------
\ QUESTION-NODE tests
\ ---------------------------------------------------------------------------

S" Wolf" ANIMAL-NODE NODE-1
' NODE-1 ' NODE-0 S" Is it a mammal?" QUESTION-NODE NODE-2

T{ ' NODE-2 node-num -> 2 }T
T{ ' NODE-2 >BODY Q-YES @ -> ' NODE-1 }T
T{ ' NODE-2 >BODY Q-NO  @ -> ' NODE-0 }T
T{ ' NODE-2 >BODY Q-TEXT @  ' NODE-2 >BODY Q-TLEN @  s" Is it a mammal?" COMPARE -> 0 }T

VARIABLE t-root
' NODE-2 t-root !

reset-yn
TRUE push-yn      \ mammal? -> YES
TRUE push-yn      \ Is it a Wolf? -> YES
T{ t-root -1 FALSE dispatch-node -> TRUE }T

reset-yn
FALSE push-yn     \ mammal? -> NO
TRUE push-yn      \ Is it a Dog? -> YES
T{ t-root -1 FALSE dispatch-node -> TRUE }T

\ ---------------------------------------------------------------------------
\ PATCH-YES / PATCH-NO
\ ---------------------------------------------------------------------------

S" Parrot" ANIMAL-NODE NODE-3
' NODE-3 ' NODE-2 PATCH-YES

T{ ' NODE-2 >BODY Q-YES @ -> ' NODE-3 }T   \ rebound
T{ ' NODE-2 >BODY Q-NO  @ -> ' NODE-0 }T   \ untouched

S" Lizard" ANIMAL-NODE NODE-4
' NODE-4 ' NODE-2 PATCH-NO

T{ ' NODE-2 >BODY Q-NO @ -> ' NODE-4 }T    \ rebound
T{ ' NODE-2 >BODY Q-YES @ -> ' NODE-3 }T   \ untouched

CR .( test-node.fs: all tests passed ) CR
