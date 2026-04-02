# Logger Event ID Snapshot

The file `tests/Elastic.OpenTelemetry.Tests/Diagnostics/event-id-snapshot.json` is a checked-in snapshot of all `[LoggerMessage]` event IDs and names across the codebase. The test `LoggerMessageEventIdTests.AllLoggerMessageEventIds_MatchSnapshot` guards against accidental renumbering or renaming of event IDs that downstream integration tests depend on.

## When to regenerate

Regenerate the snapshot whenever you add, remove, or rename a `[LoggerMessage]` method. The test will fail with a clear message indicating what is missing or mismatched.

## How to regenerate

```bash
REGENERATE_EVENT_ID_SNAPSHOT=true dotnet test -c release --filter "FullyQualifiedName~LoggerMessageEventIdTests.AllLoggerMessageEventIds_MatchSnapshot"
```

This overwrites `event-id-snapshot.json` in-place with the current runtime state. Commit the updated file alongside your code change.

## Rules for event IDs

- IDs must be unique across all `[LoggerMessage]` methods project-wide (enforced by `AllLoggerMessageEventIds_AreUnique`)
- IDs must also be unique across conditional compilation boundaries (enforced by `AllLoggerMessageEventIds_AreUnique_AcrossConditionalCompilation`)
- IDs are grouped by functional area — see existing ranges in the snapshot as a guide
