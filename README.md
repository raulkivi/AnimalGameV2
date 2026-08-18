# Animal Game V2

A "20 questions" style guessing game written in **Forth** (gforth). The
computer tries to guess the animal you're thinking of by asking yes/no
questions. When it guesses wrong, you teach it a new animal and a question
that tells the two apart - so the program gets smarter every time you
play.

What makes this version distinctive: the decision tree doesn't live as a
heap struct in a custom save format. Every learned animal or question
becomes a live word in the Forth dictionary, and the save file is literal
Forth source that rebuilds those words on load.

## Contents

- [Requirements](#requirements)
- [Running](#running)
- [How it works](#how-it-works)
- [Tests](#tests)
- [A bit about Forth](#a-bit-about-forth-createdoes)
- [License](#license)
- [Sources & further reading](#sources--further-reading)

## Requirements

- [gforth](https://gforth.org/) - built and tested against 0.7.3.

```bash
# Debian/Ubuntu
sudo apt install gforth
```

## Running

Run all commands **from the project root** - `data/rules.fs` is opened
relative to the gforth process's working directory.

```bash
make run     # play the game
make test    # run all unit tests plus a two-process persistence check
make clean   # delete the learned rules file (data/rules.fs)
```

Individual test suites:

```bash
make test-node
make test-ui
make test-tree
make test-persist
make test-integration   # writes+replays a scratch rules file across two real gforth processes
```

## How it works

- **Nodes are words, not structs.** `ANIMAL-NODE` and `QUESTION-NODE` are
  `CREATE...DOES>` factory words - executing the word a factory produces
  walks its own subtree and leaves a win/lose flag. There's no leaf/question
  type tag checked during traversal; the word already knows what it does.
- **Learning generates source, not a struct patch.** On a wrong guess,
  `learn` synthesizes a few lines of Forth (new node definitions, plus a
  `PATCH-YES`/`PATCH-NO` rebind if the replaced leaf wasn't the root),
  `EVALUATE`s them immediately against the live dictionary, and appends the
  same text to `data/rules.fs`. Nothing is rewritten in full - a correct
  guess writes nothing at all.
- **Loading replays the file.** The next launch reads `data/rules.fs`
  (`OPEN-FILE`/`READ-LINE`, one line at a time) and `EVALUATE`s each line -
  interpreting the file rebuilds the whole dictionary. There is no custom
  parser; the same words (`ANIMAL-NODE`, `QUESTION-NODE`, `PATCH-YES`,
  `PATCH-NO`) that a live learn calls are exactly what the save file calls.
  (Not gforth's `INCLUDED`: it resolves a relative path through its own
  search path rather than the process's working directory, which doesn't
  match how the rest of this file already does I/O - reading the file
  directly keeps path resolution consistent everywhere.)
- **All player-facing I/O goes through three `DEFER` words** (`ASK-YESNO`,
  `PROMPT-LINE`, `DISPLAY`) in `ui.fs`, so the game engine has zero
  dependency on a real terminal - tests swap in scripted answers. The one
  extra prompt beyond a plain yes/no game: text containing a double-quote
  character is rejected and re-asked, since player text is spliced into
  generated Forth source and a stray `"` would otherwise let it break out
  of a string literal.

See [`docs/AnimalGameV2.md`](docs/AnimalGameV2.md) for the full design
specification - data structure, game loop, learning algorithm, module
layout and word-level API, and the persistence format.

## Tests

Unit tests use gforth's built-in `T{ ... }T` assertion harness (`tester.fs`)
and override `ui.fs`'s `DEFER` words with scripted answers. `persist.fs`
and `tree.fs`'s tests redirect `RULES-PATH` to a scratch file so no test
ever touches the real `data/rules.fs`. `test-integration` goes further: it
runs two genuinely separate gforth processes - one learns and writes a
rules file, a fresh one cold-loads it - since that's the one guarantee
("the game remembers what it learned last session") that can't be verified
within a single process. All suites pass:

```bash
$ make test
test-node.fs: all tests passed
test-ui.fs: all tests passed
test-tree.fs: all tests passed
test-persist.fs: all tests passed
round1-learn.fs: seeded + learned Wolf, rules file written
round2-verify-restart.fs: cold-start replay reached Wolf and Dog
```

## A bit about Forth: `CREATE...DOES>`

This game leans on one specific Forth feature: `CREATE...DOES>` lets you
define a *factory* word that, each time it runs, creates a new named word
whose behavior is whatever code follows `DOES>`. That's what makes "a node
is a word" workable - `ANIMAL-NODE` and `QUESTION-NODE` are themselves
ordinary words, but calling them manufactures more words, each carrying
its own data and its own custom behavior. It's the same "extending the
language is just defining more words" idea Forth is built on generally -
this design just leans on it to make the *data* extend the language too,
not only the code.

[Forth](https://en.wikipedia.org/wiki/Forth_(programming_language)) was
created by **Charles "Chuck" Moore** over roughly 1968–1971. He developed
the early ideas at Mohasco Industries and then, in 1971, built the first
complete standalone Forth at the U.S. **National Radio Astronomy
Observatory (NRAO)** to control the 11-metre radio telescope at Kitt
Peak - the application that made the language famous.<sup>[[1]](#ref-moore-hopl)</sup> It is a
stack-based, extensible language: you build a program by defining new
"words" in terms of existing ones, growing the language upward until it
speaks your problem domain directly. The whole system - compiler,
interpreter, and live REPL - is tiny, which made Forth a natural fit for
the small, resource-constrained computers of its era. Reading a REPL
session left to right makes the trick visible: `3 4 + .` pushes `3` and
`4` onto the stack, `+` pops both and pushes their sum, and `.` pops and
prints it - `7`. `: square dup * ;` **defines a new word** - `dup`
duplicates the top of the stack and `*` multiplies, so `square` squares
whatever it's given; `5 square .` then uses it like any built-in word.
This is the whole trick: Forth has almost no built-in syntax, only words
that operate on a stack - so "defining a word" and "extending the
language" are the same act. This game leans on exactly that trick, just
one level up: `ANIMAL-NODE` and `QUESTION-NODE` extend the language with
*data*, not only control flow.

**Forth and AI (late 1970s–1980s, the expert-systems era).** During the
expert-systems boom of the 1980s, its interactivity and radical
extensibility made it an appealing niche vehicle for AI experimentation
and robotics. Because a Forth programmer effectively *grows a
domain-specific language* (the same trait that drew people to Lisp, but in
a tiny footprint suited to embedded control), hobbyists and researchers
built small expert-system shells, rule engines, and real-time control for
autonomous robots in Forth. After the late-1980s "AI winter" that interest
faded along with the wider field. This little game is a miniature example
of that tradition: a program that starts almost knowing nothing and
extends its own decision tree from experience.

**Forth in the space industry.** Forth's tiny footprint, deterministic
real-time behaviour, and live, on-target interactivity (you can poke at a
running system over a slow telemetry link) made it a favourite for
spacecraft and embedded avionics. Moore pioneered **stack processors that
execute Forth in hardware** (his Novix NC4016); that lineage led to chips
such as the Harris/Intersil **RTX2010**, which flew on numerous missions -
including the **Philae** lander of ESA's Rosetta comet mission, where two
RTX2010s ran the command-and-data management system.<sup>[[2]](#ref-rtx-philae)</sup> From the
1980s and 1990s onward, Forth has been used in instruments and controllers
across NASA and ESA programs (Galileo, Cassini, NEAR, and others), where
small, reliable, and inspectable code matters most.<sup>[[3]](#ref-forth-space)</sup>

**Forth's influence: PostScript/Ghostscript and the Java VM.** Forth's core
idea - express computation as words operating on an operand **stack**, run
by a small, portable virtual machine - rippled out far beyond Forth
itself.

- **PostScript and Ghostscript.** Adobe's **PostScript** page-description
  language is, at heart, a stack-based, postfix (reverse-Polish)
  interpreter very much in the Forth-like tradition: you push operands and
  then apply operators, and you extend the language by defining new
  procedures in terms of existing ones. John Warnock and Chuck Geschke
  drew on that stack-based model - by way of their earlier Interpress and
  "Design System" work - when they founded Adobe in 1982 and shipped
  PostScript in 1984.<sup>[[4]](#ref-postscript)</sup> (Its genealogy runs through that Design
  System / Interpress line rather than directly from Forth, but the two
  share the same stack-and-postfix spirit.) **[Ghostscript](https://www.ghostscript.com/)**,
  first released by L. Peter Deutsch in 1988, is the long-lived
  open-source interpreter for PostScript and PDF - so every time
  Ghostscript renders a `.ps` file, it is executing a stack language in
  that same lineage.<sup>[[5]](#ref-ghostscript)</sup>

- **Java and the JVM.** Java compiles to **bytecode** that runs on the
  **Java Virtual Machine**, and the JVM is itself a *stack machine*:
  instructions like `iload`, `iadd`, and `invokevirtual` push and pop
  values on an operand stack rather than naming registers.<sup>[[6]](#ref-jvm-spec)</sup>
  Stack machines actually predate Forth (the Burroughs B5000 dates to
  1961), but Forth popularized the "compile to a compact, portable,
  stack-based bytecode run by a tiny VM" approach in software, and its
  threaded code and inner interpreter are close cousins of later bytecode
  VMs.<sup>[[7]](#ref-threaded)</sup> The JVM didn't copy Forth directly - it's convergent
  design - but it shares the same stack-VM philosophy.

The through-line: Forth showed that a stack-oriented virtual machine can
be small, portable, and easy to implement - a lesson that PostScript
adopted almost literally and that the JVM (and CPython, WebAssembly, and
others) carried into the mainstream.

## License

[MIT](LICENSE)

## Sources & further reading

1. <a id="ref-moore-hopl"></a>Charles H. Moore, *The Evolution of FORTH* (ACM HOPL II) -
   [colorforth.github.io/HOPL.html](https://colorforth.github.io/HOPL.html);
   *Forth (programming language)* -
   [en.wikipedia.org/wiki/Forth\_(programming\_language)](https://en.wikipedia.org/wiki/Forth_(programming_language)).
2. <a id="ref-rtx-philae"></a>*RTX2010* -
   [en.wikipedia.org/wiki/RTX2010](https://en.wikipedia.org/wiki/RTX2010);
   "Here Comes Philae - Powered by an RTX2010", CPU Shack -
   [cpushack.com/2014/11/12/here-comes-philae-powered-by-an-rtx2010](https://www.cpushack.com/2014/11/12/here-comes-philae-powered-by-an-rtx2010/).
3. <a id="ref-forth-space"></a>"Space Applications", Forth, Inc. -
   [forth.com/resources/space-applications](https://www.forth.com/resources/space-applications/).
4. <a id="ref-postscript"></a>*PostScript* -
   [en.wikipedia.org/wiki/PostScript](https://en.wikipedia.org/wiki/PostScript)
   (Adobe founded 1982; PostScript released 1984).
5. <a id="ref-ghostscript"></a>*Ghostscript* -
   [en.wikipedia.org/wiki/Ghostscript](https://en.wikipedia.org/wiki/Ghostscript)
   (first released by L. Peter Deutsch, 1988).
6. <a id="ref-jvm-spec"></a>*The Java Virtual Machine Specification*, §2.6 (Frames / operand
   stack) -
   [docs.oracle.com/javase/specs/jvms/se22/html/jvms-2.html](https://docs.oracle.com/javase/specs/jvms/se22/html/jvms-2.html).
7. <a id="ref-threaded"></a>*Stack machine* -
   [en.wikipedia.org/wiki/Stack\_machine](https://en.wikipedia.org/wiki/Stack_machine);
   *Threaded code* -
   [en.wikipedia.org/wiki/Threaded\_code](https://en.wikipedia.org/wiki/Threaded_code).
