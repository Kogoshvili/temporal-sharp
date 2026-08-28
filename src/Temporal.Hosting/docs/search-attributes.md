# Search attributes

Search attributes are custom, typed key/value metadata you attach to workflow
executions so they can be queried. Before you can set or query one, it must
exist on the server's namespace — normally an ops step
(`temporal operator search-attribute create ...`). This library bootstraps them:
declared attributes are registered once at startup by `SearchAttributeRegistrar`
(after the connection waiter), idempotently, across the default namespace and
every `Temporal:Namespaces` entry.

Attributes are declared under `Temporal:SearchAttributes`, keyed by name with an
indexed value type:

```json
{
  "Temporal": {
    "SearchAttributes": {
      "Enabled": true,
      "FailOnConflict": false,
      "Attributes": {
        "CustomerId": { "Type": "Keyword" },
        "Amount":     { "Type": "Double" }
      }
    }
  }
}
```

- `Enabled` (default `true`) — turn off registration where an environment lacks
  operator permission.
- `FailOnConflict` (default `false`) — when an attribute already exists with a
  different type, fail startup (`true`) rather than log a warning (`false`).
- `Attributes` — name → `Type` (`Keyword`, `Text`, `Int`, `Double`, `Bool`,
  `Datetime`, `KeywordList`).

Registration is **add-only and idempotent**: missing attributes are added, and
declared attributes are never removed.

This only bootstraps the *keys*. Setting *values* is done by your workflow code
through the SDK, either at start via `WorkflowOptions.TypedSearchAttributes` (or
`ChildWorkflowOptions.TypedSearchAttributes`) or during execution via
`Workflow.UpsertTypedSearchAttributes(...)`.

## Imperative control

`ISearchAttributeOps` is the client-side facade (injected singleton) over the
SDK's operator service. `EnsureAsync` is the idempotent core; `ListAsync` and
`RemoveAsync` are pass-throughs for parity:

```csharp
await searchAttributeOps.EnsureAsync(
    "default",
    new Dictionary<string, IndexedValueType>
    {
        ["CustomerId"] = IndexedValueType.Keyword,
        ["Amount"] = IndexedValueType.Double,
    },
    failOnConflict: false);

var existing = await searchAttributeOps.ListAsync("default");
```
