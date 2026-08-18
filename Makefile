# Makefile — Animal Game V2 (gforth)
#
# gforth's own process exit code does NOT reflect a T{ ... }T assertion
# failure (tester.fs just prints "INCORRECT RESULT" and keeps going) —
# only an actual crash (unhandled THROW) sets it. run-test below checks
# both: the exit code (catches crashes) and a grep for "INCORRECT RESULT"
# (catches assertion failures), so `make test` fails loudly either way.

GFORTH   = gforth
SRC_MAIN = src/main.fs
OUT_DIR  = /tmp/animalgamev2-test-output

.PHONY: run test test-node test-ui test-tree test-persist test-integration clean

define run-test
	@mkdir -p $(OUT_DIR)
	@$(GFORTH) $(1) -e bye > $(OUT_DIR)/$(2).out 2>&1; status=$$?; \
	cat $(OUT_DIR)/$(2).out; \
	if [ $$status -ne 0 ] || grep -q "INCORRECT RESULT" $(OUT_DIR)/$(2).out; then \
		echo "FAILED: $(1)"; exit 1; \
	fi
endef

run:
	$(GFORTH) $(SRC_MAIN)

test: test-node test-ui test-tree test-persist test-integration

test-node:
	$(call run-test,tests/test-node.fs,test-node)

test-ui:
	$(call run-test,tests/test-ui.fs,test-ui)

test-tree:
	$(call run-test,tests/test-tree.fs,test-tree)

test-persist:
	$(call run-test,tests/test-persist.fs,test-persist)

# Two genuinely separate gforth processes: round 1 learns and writes a
# scratch rules file for real; round 2 starts an empty dictionary from
# scratch and cold-loads it, proving persistence survives a real restart
# and not just a live EVALUATE within one process.
test-integration:
	@rm -f /tmp/animalgamev2-integration-rules.fs
	$(call run-test,tests/integration/round1-learn.fs,integration-round1)
	$(call run-test,tests/integration/round2-verify-restart.fs,integration-round2)

clean:
	rm -f data/rules.fs data/rules.fs.tmp
