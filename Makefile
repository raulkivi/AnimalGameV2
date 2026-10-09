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

.PHONY: run test test-node test-ui test-tree test-persist test-integration clean

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
	$(GFORTH) $(SRC_MAIN)

test: test-node test-ui test-tree test-persist test-integration

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

clean:
	rm -f data/rules.fs data/rules.fs.tmp
