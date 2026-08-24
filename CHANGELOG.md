# Changelog

All notable changes to this project are documented in this file. Each section
summarizes the commits since the previous release, and is generated
automatically when a release is prepared.

## [1.0.0-beta.5] - 2026-08-24

- Fix TMP2124 false positive when cleanup token is in a helper method
- Make TMP3104 (unnecessary heartbeat) off by default
- Remove namespace explanation from README

## [1.0.0-beta.4] - 2026-08-24

- Prepare v1.0.0-beta.4 release
- Address deferred remediation items (TMP2124/2146/2144/2101/0112/2172)
- Remediate rules against Temporal skill docs and SDK
- Fix heartbeat false positives and refine TMP2123/TMP3104

## [1.0.0-beta.3] - 2026-08-24

- Prepare v1.0.0-beta.3 release
- Remove no-basis rules and correct signal return-type rule
- Fix analyzer false positives against Temporal docs
- Check out repo in publish release job so gh can generate notes

## [1.0.0-beta.2] - 2026-08-23

- Trigger publish from release workflow; allow manual publish dispatch
- Prepare v1.0.0-beta.2 release
- Fix release workflow: commit changelog before rebase
- Reduce analyzer false positives across the benchmark suite
- Address review.md findings (third pass)
- Address review.md findings (second pass)
- Address review.md findings
- Fix false positives found validating against real-world repos
- Address review findings: preset severities, reachability, rule correctness
- Fix alternative tool links and wording in README
- Address review findings: SDK API naming, mutation detection, idempotency, property queries
- Fix incorrect SDK API references in rules and code fixes
- Reword TMP2132 for non-ApplicationFailure exception coverage
- Address review findings across analyzers and code fixes
- Add Mutex/Semaphore -> Temporalio.Workflows code fix
- Add P4 cross-cutting features
- Add auto-generated CHANGELOG.md and release workflow
- Add P3 best-practice and comment rules
- Add P2 contract, lifecycle, payload, activity, and versioning rules
- Add P1 determinism rules
- Add RULES.md docs generator and GitHub workflows
- Add P0 query/signal rules, floating-task rule, and code fixes
- Correct shipped rules and add BestPractice/Testing categories

## [1.0.0-beta.1] - 2026-08-23

- Add NuGet publish pipeline and versioning
- Rename project to Kogoshvili.Temporal
- Extend TMP0111 to synchronous task waits
- Expand sample app to demonstrate all 38 rules
- Fix CI smoke test: downgrade sample analyzer rules to warning
- Add TMP0161: culture-sensitive parse/format in workflow code
- Add TMP2103/TMP2104: WaitConditionAsync timeout checks
- Link RULES.md from README
- Add TMP3203: activity method mutates instance state
- Add TMP0144, TMP0145, TMP1106 rules
- Expand TMP0142 to cover WaitHandle and ReaderWriterLock
- Drop rule count from RULES.md and remove README Status section
- Add TMP1105: static state mutated via method call
- Extend TMP0151 to LINQ materialization and dictionary key/value views
- Add TMP0143: raw task scheduling in workflow code
- Fix TMP1104 and TMP3201 false positives
- Rework severities, split TMP3301, and replace ignore marker with standard suppression
- Expand heartbeat rules and escalate TMP3102 to error
- Fix TMP3202 false positive and make TMP2111 opt-in
- Polish README: install section, configs, alternatives, move disclaimer
- Rename workflowcheck:ignore suppression marker to temporalsharp:ignore
- Add rule catalog
- Move roadmap todo to README and drop plan.md reference
- Implement v3 rules, call-graph fidelity, solution-level graph, and search_attributes config
- Implement v2 rules, CLI severity overrides, and workflowcheck:ignore suppression
- Enable NuGet audit and fix transitive dependency vulnerabilities
- Add CI workflow
- Add sample app and package readme for NuGet
- Add CLI with console/json/sarif reporters and --fail-on exit codes
- Add workflow-state and SDK feature-misuse analyzers (TMP1101/2101/2111/2121/2131)
- Add determinism analyzer (TMP0101/0111/0121/0131) with tests
- Add analyzer core: workflow detection, call graph, deny-list, descriptors
- Scaffold solution: analyzer, CLI, tests with central package management

