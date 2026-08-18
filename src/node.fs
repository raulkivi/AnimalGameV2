\ node.fs — Node-as-word: ANIMAL-NODE / QUESTION-NODE factories
\
\ A node is not a heap struct but a CREATE...DOES> dictionary word.
\ Executing a node's word walks its own subtree and leaves a
\ won-flag; there is no runtime "is this a leaf?" check anywhere in this
\ file or in tree.fs — the word's own DOES> action already knows what kind
\ of node it is.
\
\ Every node word's DOES> action has stack effect:
\   ( cell-addr parent-num branch-is-yes -- won-flag )
\ where cell-addr is the address of the mutable cell that currently holds
\ this node's xt (either GAME-ROOT-CELL, or a QUESTION-NODE's own Q-YES/
\ Q-NO field), parent-num is the num of the QUESTION-NODE that reached this
\ node (-1 if this node is the root), and branch-is-yes says which branch
\ of that parent it is. A leaf (ANIMAL-NODE) needs all three only to hand
\ them to LEARN-HOOK on a wrong guess, so it can synthesize a replayable
\ patch instruction; a QUESTION-NODE ignores its own incoming triple
\ entirely and constructs a fresh one for whichever child it recurses into.

REQUIRE ui.fs

DECIMAL

\ ---------------------------------------------------------------------------
\ Node layout
\ ---------------------------------------------------------------------------
\ Both structures share the same (KIND XT TEXT TLEN NUM ...) prefix so a
\ single accessor works on either kind — see NODE-NUM and NODE-LEAF? below.
\ KIND is read only by NODE-LEAF? (introspection, mainly for tests); the
\ actual traversal dispatch in DOES> never inspects it.

0 CONSTANT NODE-ANIMAL
1 CONSTANT NODE-QUESTION

BEGIN-STRUCTURE ANIMAL-SIZE
  FIELD: A-KIND     \ NODE-ANIMAL — introspection only, unused by DOES>
  FIELD: A-XT       \ this word's own execution token (via LATESTXT)
  FIELD: A-TEXT     \ heap address of the animal name
  FIELD: A-TLEN     \ byte length of the name
  FIELD: A-NUM      \ this node's NODE-<n> identity, for replay/patch text
END-STRUCTURE

BEGIN-STRUCTURE QUESTION-SIZE
  FIELD: Q-KIND     \ NODE-QUESTION — introspection only, unused by DOES>
  FIELD: Q-XT       \ this word's own execution token
  FIELD: Q-TEXT     \ heap address of the question text
  FIELD: Q-TLEN     \ byte length of the question
  FIELD: Q-NUM      \ this node's NODE-<n> identity
  FIELD: Q-YES      \ mutable cell: yes-child's xt
  FIELD: Q-NO       \ mutable cell: no-child's xt
END-STRUCTURE

\ node-leaf?  ( xt -- flag )  introspection only; traversal never calls this
: node-leaf? ( xt -- flag ) >BODY A-KIND @ NODE-ANIMAL = ;

\ ---------------------------------------------------------------------------
\ Node registry — NODE-<n> identity <-> xt, so a num alone (as threaded
\ through traversal, or read back from a save file being replayed) can
\ always recover the live word that num refers to.
\ ---------------------------------------------------------------------------

4096 CONSTANT MAX-NODES
CREATE node-registry MAX-NODES CELLS ALLOT
VARIABLE node-count
0 node-count !

: register-self ( xt -- num )
  node-count @
  DUP >R
  CELLS node-registry + !
  R>
  DUP 1+ node-count !
;

: xt-of ( num -- xt ) CELLS node-registry + @ ;

\ GAME-ROOT-CELL — the mutable cell holding the tree's current root xt.
\ Lives here (rather than main.fs) because replayed data/rules.fs source
\ references it by name directly, and node.fs is guaranteed to load
\ before persist.fs/tree.fs/main.fs.
VARIABLE GAME-ROOT-CELL

\ node-num  ( xt -- n )  works for either an ANIMAL-NODE or QUESTION-NODE
\ xt, since NUM sits at the same offset in both structures.
: node-num ( xt -- n ) >BODY A-NUM @ ;

\ predicted-num  ( -- n )  the num the *next* ANIMAL-NODE/QUESTION-NODE
\ call will be assigned — used by persist.fs to name a node before it
\ exists, so the generated source and the live word agree.
: predicted-num ( -- n ) node-count @ ;

\ ---------------------------------------------------------------------------
\ Shared helpers
\ ---------------------------------------------------------------------------

: copy-str ( c-addr u -- c-addr2 )
  DUP ALLOCATE THROW          \ allocate u bytes;  ( src u dest )
  DUP >R                      \ save dest             R: dest
  SWAP MOVE                   \ ( )
  R>                          \ ( dest )
;

\ dispatch-node  ( cell-addr parent-num branch-is-yes -- won-flag )
\ Fetches the xt stored at cell-addr and EXECUTEs it, leaving the triple in
\ place underneath for the node's own DOES> action to consume.
: dispatch-node ( cell-addr parent-num branch-is-yes -- won-flag )
  2 PICK @ EXECUTE
;

CREATE guess-buf 320 ALLOT
: build-guess-q ( text-addr text-len -- addr len )
  0 guess-buf C!
  s" Is it a " guess-buf +PLACE
  guess-buf +PLACE
  s" ?" guess-buf +PLACE
  guess-buf COUNT
;

: a-text$ ( a-body -- addr len ) DUP A-TEXT @ SWAP A-TLEN @ ;
: q-text$ ( q-body -- addr len ) DUP Q-TEXT @ SWAP Q-TLEN @ ;

\ ---------------------------------------------------------------------------
\ LEARN-HOOK — forward reference to tree.fs's `learn`, bound with IS once
\ tree.fs loads. Kept here (rather than a direct call) because node.fs must
\ not depend on tree.fs: tree.fs depends on node.fs, not the reverse.
\ ---------------------------------------------------------------------------

DEFER learn-hook   \ ( cell-addr old-xt parent-num branch-is-yes -- )

: default-learn-hook ( cell-addr old-xt parent-num branch-is-yes -- )
  2DROP 2DROP
  s" learn-hook not bound" DISPLAY
;
' default-learn-hook IS learn-hook

\ ---------------------------------------------------------------------------
\ ANIMAL-NODE  ( c-addr u "name" -- )
\ ---------------------------------------------------------------------------

: ANIMAL-NODE ( c-addr u "name" -- )
  CREATE
    NODE-ANIMAL ,                       \ A-KIND
    LATESTXT DUP register-self DROP ,   \ A-XT, and register num -> xt
    DUP >R
    copy-str ,                          \ A-TEXT
    R> ,                                \ A-TLEN
    node-count @ 1- ,                   \ A-NUM (the num just assigned)
  DOES> ( cell-addr parent-num branch-is-yes body -- won-flag )
    >R                                          \ R: body
    R@ a-text$ build-guess-q ASK-YESNO
    IF
      2DROP DROP
      s" I win!" DISPLAY
      R> DROP
      TRUE
    ELSE
      R> A-XT @
      -ROT                                      \ ( cell-addr old-xt parent-num branch-is-yes )
      learn-hook
      FALSE
    THEN
;

\ ---------------------------------------------------------------------------
\ QUESTION-NODE  ( yes-xt no-xt c-addr u "name" -- )
\ ---------------------------------------------------------------------------

: QUESTION-NODE ( yes-xt no-xt c-addr u "name" -- )
  CREATE
    NODE-QUESTION ,                     \ Q-KIND
    LATESTXT DUP register-self DROP ,   \ Q-XT
    DUP >R
    copy-str ,                          \ Q-TEXT
    R> ,                                \ Q-TLEN
    node-count @ 1- ,                   \ Q-NUM
    SWAP ,                              \ Q-YES = yes-xt
    ,                                   \ Q-NO  = no-xt
  DOES> ( cell-addr parent-num branch-is-yes body -- won-flag )
    >R                                          \ R: body — own incoming triple is unused
    2DROP DROP
    R@ q-text$ ASK-YESNO
    IF
      R@ Q-YES  R@ Q-NUM @  TRUE   dispatch-node
    ELSE
      R@ Q-NO   R@ Q-NUM @  FALSE  dispatch-node
    THEN
    R> DROP
;

\ ---------------------------------------------------------------------------
\ PATCH-YES / PATCH-NO — rebind one child cell of an existing QUESTION-NODE
\ without redefining it. Used when the leaf being replaced was not the
\ root (the root case rebinds GAME-ROOT-CELL directly instead).
\ ---------------------------------------------------------------------------

: PATCH-YES ( new-child-xt parent-xt -- ) >BODY Q-YES ! ;
: PATCH-NO  ( new-child-xt parent-xt -- ) >BODY Q-NO  ! ;
