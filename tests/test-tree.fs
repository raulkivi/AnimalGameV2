\ test-tree.fs — Unit tests for src/tree.fs
\
\ Scripts ui.fs's DEFER words and redirects persist.fs's RULES-PATH to a
\ scratch file so `learn` (bound as node.fs's learn-hook) can be exercised
\ without any real terminal input or touching the real data/rules.fs.

REQUIRE test/tester.fs
REQUIRE ../src/tree.fs

DECIMAL

s" /tmp/animalgamev2-test-tree-rules.fs" 2CONSTANT TEST-RULES-PATH
TEST-RULES-PATH R/W CREATE-FILE THROW CLOSE-FILE THROW
TEST-RULES-PATH set-rules-path

\ ---------------------------------------------------------------------------
\ Scripted I/O
\ ---------------------------------------------------------------------------

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
: reset-str ( -- ) 0 str-wr ! 0 str-rd ! ;
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

: reset-scripts ( -- ) reset-yn reset-str ;

\ ---------------------------------------------------------------------------
\ Test 1: traversal reaches the correct leaf, both branches
\ ---------------------------------------------------------------------------

s" Wolf"   persist-new-animal CONSTANT tr-wolf
s" Parrot" persist-new-animal CONSTANT tr-parrot
tr-wolf tr-parrot s" Is it a mammal?" persist-new-question CONSTANT tr-root
tr-root persist-set-root

reset-scripts
TRUE  push-yn    \ mammal? -> YES
TRUE  push-yn    \ Is it a Wolf? -> YES (correct guess)
T{ GAME-ROOT-CELL traverse -> TRUE }T

reset-scripts
FALSE push-yn    \ mammal? -> NO
TRUE  push-yn    \ Is it a Parrot? -> YES (correct guess)
T{ GAME-ROOT-CELL traverse -> TRUE }T

\ ---------------------------------------------------------------------------
\ Test 2: learning at the root — wrong guess triggers tree mutation
\
\ Root is currently tr-root (a QuestionNode). Learn under its YES branch
\ (currently tr-wolf): player thinks of "Fox", question "Does it bark?",
\ answer NO for the new animal -> Fox becomes the NO child, Wolf stays YES.
\ ---------------------------------------------------------------------------

reset-scripts
TRUE  push-yn                       \ mammal? -> YES (reach tr-wolf)
FALSE push-yn                       \ Is it a Wolf? -> NO (wrong guess)
s" Fox" push-str                    \ new animal name
s" Does it bark?" push-str          \ distinguishing question
FALSE push-yn                       \ new animal (Fox) answers NO

T{ GAME-ROOT-CELL traverse -> FALSE }T   \ round ends on the wrong guess

T{ GAME-ROOT-CELL @ -> tr-root }T        \ root itself is unchanged (Wolf wasn't root)
T{ tr-root >BODY Q-YES @ node-leaf? -> FALSE }T   \ Wolf's old slot is now a question
T{ tr-root >BODY Q-YES @ >BODY Q-NO @ >BODY A-TEXT @
   tr-root >BODY Q-YES @ >BODY Q-NO @ >BODY A-TLEN @
   s" Fox" COMPARE -> 0 }T             \ Fox landed on the NO branch as scripted
T{ tr-root >BODY Q-YES @ >BODY Q-YES @ -> tr-wolf }T   \ Wolf kept the YES branch

\ Fox is reachable through a fresh traversal
reset-scripts
TRUE  push-yn    \ mammal? -> YES
FALSE push-yn    \ Does it bark? -> NO
TRUE  push-yn    \ Is it a Fox? -> YES
T{ GAME-ROOT-CELL traverse -> TRUE }T

\ Wolf is still reachable, untouched by the Fox split
reset-scripts
TRUE  push-yn    \ mammal? -> YES
TRUE  push-yn    \ Does it bark? -> YES
TRUE  push-yn    \ Is it a Wolf? -> YES
T{ GAME-ROOT-CELL traverse -> TRUE }T

CR .( test-tree.fs: all tests passed ) CR
