\ tree.fs — traversal entry point and learning orchestration
\
\ traverse collapses to a single fetch-and-EXECUTE: each node's own
\ DOES> action already knows whether it's a leaf or a question, so there
\ is nothing left for this file to dispatch on. `learn` is bound as
\ node.fs's `learn-hook`, called only when an ANIMAL-NODE's guess is
\ wrong; it collects the three player inputs (guarding against embedded
\ `"` characters, since that text will be spliced into generated Forth
\ source by persist.fs) and hands off node creation and durability to
\ persist.fs's PERSIST-* words.

REQUIRE node.fs
REQUIRE ui.fs
REQUIRE persist.fs

DECIMAL

\ traverse  ( cell-addr -- won-flag )
\ cell-addr is the address of the mutable cell holding the current node's
\ xt (GAME-ROOT-CELL for the very first call). -1/FALSE mark "no parent" —
\ the root is never reached via a QUESTION-NODE branch.
: traverse ( cell-addr -- won-flag )
  -1 FALSE dispatch-node
;

\ ---------------------------------------------------------------------------
\ Injection-safe input collection
\ ---------------------------------------------------------------------------

: prompt-safe-line ( prompt-addr prompt-u -- addr u )
  BEGIN
    2DUP PROMPT-LINE
    2DUP contains-quote?
  WHILE
    2DROP
    s" That text can't contain a double-quote character -- please try again." DISPLAY
  REPEAT
  2SWAP 2DROP
;

\ ---------------------------------------------------------------------------
\ learn  ( cell-addr old-xt parent-num branch-is-yes -- )
\ ---------------------------------------------------------------------------

VARIABLE learn-cell
VARIABLE learn-old-xt
VARIABLE learn-parent-num
VARIABLE learn-branch
VARIABLE learn-new-leaf-xt

: learn ( cell-addr old-xt parent-num branch-is-yes -- )
  learn-branch !
  learn-parent-num !
  learn-old-xt !
  learn-cell !

  s" I give up!  What animal were you thinking of?" DISPLAY
  s" Animal name: " prompt-safe-line
  persist-new-animal learn-new-leaf-xt !

  s" Give me a yes/no question that tells the new animal from my guess:" DISPLAY
  s" Question: " prompt-safe-line                    ( q-addr q-len )
  2>R                                                 \ R: q-addr q-len

  s" For the new animal, is the answer to your question YES?  " ASK-YESNO
  IF
    learn-new-leaf-xt @  learn-old-xt @                ( yes-xt no-xt )
  ELSE
    learn-old-xt @  learn-new-leaf-xt @                ( yes-xt no-xt )
  THEN
  2R>                                                  ( yes-xt no-xt q-addr q-len )
  persist-new-question                                ( new-node-xt )

  DUP learn-cell @ !                                  \ live patch: root or interior cell alike

  learn-parent-num @ -1 = IF
    persist-set-root
  ELSE
    learn-parent-num @ xt-of                          ( new-node-xt parent-xt )
    learn-branch @ IF
      persist-patch-yes
    ELSE
      persist-patch-no
    THEN
  THEN
;
' learn IS learn-hook
