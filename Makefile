# Makefile — Animal Game V2 (gforth)
#
# gforth's own process exit code does NOT reflect a T{ ... }T assertion
# failure (tester.fs just prints "INCORRECT RESULT" and keeps going) —
# only an actual crash (unhandled THROW) sets it. run-test below checks
# both: the exit code (catches crashes) and a grep for "INCORRECT RESULT"
# (catches assertion failures), so `make test` fails loudly either way.

#
# Every test recipe gets its own `mktemp -d` scratch directory, passed to
# gforth as ANIMALGAMEV2_TMP (see tests/tmp-path.fs) and removed afterwards,
# so runs never share fixed /tmp paths.

GFORTH   = gforth
SRC_MAIN = src/main.fs

.PHONY: run test test-node test-ui test-tree test-persist test-integration test-fresh-clone clean

# run-test(file, name) — one shell command list; expects $$tmp to hold the
# recipe's scratch directory (set up by with-tmp below).
define run-test
ANIMALGAMEV2_TMP=$$tmp $(GFORTH) $(1) -e bye > $$tmp/$(2).out 2>&1; status=$$?; \
	cat $$tmp/$(2).out; \
	if [ $$status -ne 0 ] || grep -q "INCORRECT RESULT" $$tmp/$(2).out; then \
		echo "FAILED: $(1)"; exit 1; \
	fi
endef

# with-tmp(commands) — runs commands with a fresh $$tmp, always cleaned up.
define with-tmp
	@tmp=$$(mktemp -d) && trap 'rm -rf "$$tmp"' EXIT && $(1)
endef

run:
	@mkdir -p data
	$(GFORTH) $(SRC_MAIN)

test: test-node test-ui test-tree test-persist test-integration test-fresh-clone

test-node:
	$(call with-tmp,{ $(call run-test,tests/test-node.fs,test-node); })

test-ui:
	$(call with-tmp,{ $(call run-test,tests/test-ui.fs,test-ui); })

test-tree:
	$(call with-tmp,{ $(call run-test,tests/test-tree.fs,test-tree); })

test-persist:
	$(call with-tmp,{ $(call run-test,tests/test-persist.fs,test-persist); })

# Two genuinely separate gforth processes: round 1 learns and writes a
# scratch rules file for real; round 2 starts an empty dictionary from
# scratch and cold-loads it, proving persistence survives a real restart
# and not just a live EVALUATE within one process.
test-integration:
	$(call with-tmp,{ $(call run-test,tests/integration/round1-learn.fs,integration-round1); } && \
	  { $(call run-test,tests/integration/round2-verify-restart.fs,integration-round2); })

# A fresh clone has no data/rules.fs (and possibly no data/ at all). Copy
# only src/ into an empty scratch directory, play one scripted learning
# round through the real main.fs from there, and check it exits cleanly
# and leaves a non-empty data/rules.fs behind.
test-fresh-clone:
	$(call with-tmp,cp -r src "$$tmp/src" && \
	  printf 'no\nWolf\nDoes it howl?\nyes\nno\n' | \
	    ( cd "$$tmp" && $(GFORTH) src/main.fs ) > "$$tmp/fresh.out" 2>&1; status=$$?; \
	  cat "$$tmp/fresh.out"; echo; \
	  if [ $$status -ne 0 ] || [ ! -s "$$tmp/data/rules.fs" ]; then \
	    echo "FAILED: fresh clone (no data/) run"; exit 1; \
	  fi; \
	  echo "fresh-clone: game ran from a checkout without data/ and created data/rules.fs")

clean:
	rm -f data/rules.fs data/rules.fs.tmp
