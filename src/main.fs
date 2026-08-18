\ main.fs — entry point and game loop

REQUIRE tree.fs
REQUIRE persist.fs

DECIMAL

: init-game ( -- )
  load-words
;

: play-round ( -- )
  GAME-ROOT-CELL traverse DROP
;

: game-loop ( -- )
  BEGIN
    play-round
    s" Would you like to play again?" ASK-YESNO
  WHILE
  REPEAT
;

: run-game ( -- )
  init-game
  game-loop
;

run-game
BYE
