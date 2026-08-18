\ ui.fs — Abstract I/O layer via DEFER words
\
\ All user interaction goes through three DEFER words so the game engine
\ has zero dependency on concrete I/O, and tests can redefine these words
\ with scripted answers instead of a real terminal.

DECIMAL

256 CONSTANT UI-BUFSIZE

CREATE ui-buf UI-BUFSIZE ALLOT
CREATE ui-yn-buf UI-BUFSIZE ALLOT

\ ASK-YESNO  ( c-addr u -- flag )
DEFER ASK-YESNO

\ PROMPT-LINE  ( c-addr u -- c-addr2 u2 )
DEFER PROMPT-LINE

\ DISPLAY  ( c-addr u -- )
DEFER DISPLAY

\ first-nonblank  ( c-addr u -- ch )
: first-nonblank ( c-addr u -- ch )
  0 ?DO
    DUP I + C@
    DUP BL <> IF
      NIP UNLOOP EXIT
    THEN
    DROP
  LOOP
  DROP 0
;

\ classify-yn  ( c-addr u -- yes-flag valid-flag )
: classify-yn ( c-addr u -- yes-flag valid-flag )
  first-nonblank 32 OR
  DUP [CHAR] y = IF DROP TRUE  TRUE EXIT THEN
      [CHAR] n = IF      FALSE TRUE EXIT THEN
  FALSE FALSE
;

: default-ask-yesno ( c-addr u -- flag )
  BEGIN
    2DUP TYPE ."  (yes/no): "
    ui-yn-buf UI-BUFSIZE ACCEPT
    CR
    ui-yn-buf SWAP classify-yn
    DUP 0=
  WHILE
    2DROP
  REPEAT
  DROP NIP NIP
;

: default-prompt-line ( c-addr u -- c-addr2 u2 )
  TYPE ."  "
  ui-buf UI-BUFSIZE ACCEPT
  CR
  ui-buf SWAP
;

: default-display ( c-addr u -- )
  TYPE CR
;

' default-ask-yesno  IS ASK-YESNO
' default-prompt-line IS PROMPT-LINE
' default-display     IS DISPLAY
