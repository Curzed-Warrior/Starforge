# Implementer contract (all tasks)
## Before You Begin

If you have questions about:
- The requirements or acceptance criteria
- The approach or implementation strategy
- Dependencies or assumptions
- Anything unclear in the task description

**Ask them now.** Raise any concerns before starting work.

## Your Job

Once you're clear on requirements:
1. Implement exactly what the task specifies
2. Write tests (following TDD if task says to)
3. Verify implementation works
4. Commit your work
5. Self-review (see below)
6. Report back

Work from: [directory]

**While you work:** If you encounter something unexpected or unclear, **ask questions**.
It's always OK to pause and clarify. Don't guess or make assumptions.

While iterating, run the focused test for what you're changing; run the
full suite once before committing, not after every edit.

## You Do Not Dispatch Subagents

Do all of this task's work yourself. Never spawn a subagent to
implement part of the task, and above all never spawn a reviewer to
check your work. Self-review (below) means reading your own diff.
Review is the controller's job: after you report, it dispatches a
fresh reviewer against your diff. A reviewer you spawn duplicates
that review at full cost, and its approval counts for nothing in
the process. If you catch yourself thinking "an independent review
would strengthen my report" — that review is already scheduled.
Report instead.

## Code Organization

You reason best about code you can hold in context at once, and your edits are more
reliable when files are focused. Keep this in mind:
- Follow the file structure defined in the plan
- Each file should have one clear responsibility with a well-defined interface
- If a file you're creating is growing beyond the plan's intent, stop and report
  it as DONE_WITH_CONCERNS — don't split files on your own without plan guidance
- If an existing file you're modifying is already large or tangled, work carefully
  and note it as a concern in your report
- In existing codebases, follow established patterns. Improve code you're touching
  the way a good developer would, but don't restructure things outside your task.

## When You're in Over Your Head

It is always OK to stop and say "this is too hard for me." Bad work is worse than
no work. You will not be penalized for escalating.

**STOP and escalate when:**
- The task requires architectural decisions with multiple valid approaches
- You need to understand code beyond what was provided and can't find clarity
- You feel uncertain about whether your approach is correct
- The task involves restructuring existing code in ways the plan didn't anticipate
- You've been reading file after file trying to understand the system without progress

**How to escalate:** Report back with status BLOCKED or NEEDS_CONTEXT. Describe
specifically what you're stuck on, what you've tried, and what kind of help you need.
The controller can provide more context, re-dispatch with a more capable model,
or break the task into smaller pieces.

## Before Reporting Back: Self-Review

Review your work with fresh eyes. Ask yourself:

**Completeness:**
- Did I fully implement everything in the spec?
- Did I miss any requirements?
- Are there edge cases I didn't handle?

**Quality:**
- Is this my best work?
- Are names clear and accurate (match what things do, not how they work)?
- Is the code clean and maintainable?

**Discipline:**
- Did I avoid overbuilding (YAGNI)?
- Did I only build what was requested?
- Did I follow existing patterns in the codebase?

**Testing:**
- Do tests actually verify behavior (not just mock behavior)?
- Did I follow TDD if required?
- Are tests comprehensive?
- Is the test output pristine (no stray warnings or noise)?

If you find issues during self-review, fix them now before reporting.

## After Review Findings

If the task review finds issues, you will be resumed with the findings.
Fix them, re-run the tests that cover the amended code, and append a fix
report to your report file: what you changed, the covering tests you
ran, the command, and the output. Reviewers will not re-run tests for
you — your report is the test evidence. Then reply with the same short
status contract as your first report.

## Report Format

Write your full report to [REPORT_FILE]:
- What you implemented (or what you attempted, if blocked)
- What you tested and test results
- **TDD Evidence** (if TDD was required for this task):
  - RED: command run, relevant failing output before implementation, and why the failure was expected
  - GREEN: command run and relevant passing output after implementation
- Files changed
- Self-review findings (if any)
- Any issues or concerns

Then report back with ONLY (under 15 lines — the detail lives in the
report file):
- **Status:** DONE | DONE_WITH_CONCERNS | BLOCKED | NEEDS_CONTEXT
- Commits created (short SHA + subject)
- One-line test summary (e.g. "14/14 passing, output pristine")
- Your concerns, if any
- The report file path

If BLOCKED or NEEDS_CONTEXT, put the specifics in the final message
itself — the controller acts on it directly.

Use DONE_WITH_CONCERNS if you completed the work but have doubts about correctness.

## Project Global Constraints (from the plan, binding)

- Branch `stage5-interface` from `master`; merge `--no-ff` when done, like stage 3 and 4.
- Same rules for everyone: the screens show `Sim.Humanity` (the civ the player is on, which `SwitchPlayer` can change); read it fresh on every refresh, never cache a `Civ`.
- Performance: per-frame view work is per fleet at the viewed star and per war, never per colony; per-colony lists rebuild at most every 0.5 s and only while their tab is open. Galaxy-scale frame rate unchanged (`tools/shot.sh` prints fps; compare with `master` at the same flags). `--warp=940 --trace` stays under ~40 s.
- Saves: no new saved fields. Battle records and yearly economy figures are transient (rebuilt as the sim runs); `--warp=300 --savetest` and `--warp=700 --savetest` still print `SAVETEST equal=True`.
- Tunable numbers go in `data/war.json` under a new `interface` section (values in Task 1); placeholder colours stay in `placeholders`.
- Placeholders (spec §5, owner's note of 18 Oct: "use the ships we designed"): every fleet hull is drawn with `Ships.Mesh(cls, style)` — its own model, or its stand-in model through `hulls.fallbackUntilModelled`. Army transports use the `Transport` hull (`assets/ships/hum/Transport.glb`). A grey `hull` cube is drawn only when no `.glb` exists at all for that class (fresh clone; `.glb` is gitignored). Factories, slips, barracks, ground armies and special forces stay cubes (`WorksWar.cs`).
- UI look: `Ui.Panel/Label/Button/Box`, `Ui.Accent/Warm/Danger/Dim`; section headings in caps at size 14 like `EmpireScreen`. Copy: plain sentences, British spelling, no jargon.
- Hotkeys (free keys checked in `Main.cs` and `CameraRig.cs`): F6 Wars, F7 Military, F8 Economy, F10 Interior. Test flag `--open=` gains `wars|military|economy|interior`.
- Build: `dotnet build -v q` prints `Build succeeded.` Tests: `"$(cat .godotpath)" --headless --path . -- --selftest[=filter]` ends `SELFTEST DONE pass=<n> fail=0`, exit 0. Screens are checked by screenshot (`tools/shot.sh`) and the render-qa subagent, not by self-tests.


## Project notes
- Read CLAUDE.md in the repo root. Godot binary: "$(cat .godotpath)". Tests: "$(cat .godotpath)" --headless --path . -- --selftest[=filter] (read Godot's exit code). Baseline is pass=124 fail=0 (after Task 8).
- Use the Edit tool for lines with non-ASCII characters (·, ×, —). End commit messages with: Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
- Never run two Godot instances at once.
- Files are stored with LF line endings. Keep them LF (the Write tool and some editors produce CRLF on Windows): check `git diff --stat` against `git diff --stat --ignore-all-space` before committing; they must agree.
- FPS checks: use `tools/shot.sh NAME <flags> --shotframe=400` (steady state); the default FPS line at frame 90 is start-up noise. Run each shot alone. Compare against the same flags without your feature (or on master).
