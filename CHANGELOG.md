# Changelog

All notable changes to this project are documented in this file. Each section
summarizes the commits since the previous release, and is generated
automatically when a release is prepared.

## [Unreleased]

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

