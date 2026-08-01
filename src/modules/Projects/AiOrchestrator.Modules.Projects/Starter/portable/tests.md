---
description: Write the tests this story's change needs, and run them.
---

Write tests for the story you have been given.

**Read the existing tests first.** Match how this repository already tests things — the framework,
the naming, the level (unit, integration, end to end) it chooses for this kind of behaviour. A test
that arrives in a foreign style is a test nobody maintains.

What to aim at, in order:

1. **The behaviour the story describes**, at the level this repository would naturally test it.
2. **The edges that would actually break**: the empty case, the boundary value, the concurrent
   second caller, the failure the code handles quietly.
3. Not coverage for its own sake. A test that cannot fail is worse than no test, because it reads as
   protection.

**Verify each test can fail.** Break the code it covers, confirm the test goes red *and that the
build compiled first*, then put the code back. A red that came from a compile error proves nothing.
Say in your summary which tests you checked this way.

Run the suite before you finish and report the result honestly, including failures you did not fix.
