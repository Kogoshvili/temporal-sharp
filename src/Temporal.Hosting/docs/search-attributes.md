# Search attributes

Search attributes are custom, typed key/value metadata you attach to workflow
executions so they can be queried. Before you can set or query one it must exist
on the server's namespace — normally an ops step. This library bootstraps them:
attributes declared under `Temporal:SearchAttributes` are registered once at
startup by `SearchAttributeRegistrar`, idempotently, across the default namespace
and every `Temporal:Namespaces` entry.

## Minimal setup

A pure JSON block, no C# — declare the attribute name with its indexed value
type and the library registers it at startup:

```json
{
  "Temporal": {
    "SearchAttributes": {
      "Attributes": {
        "CustomerId": { "Type": "Keyword" }
      }
    }
  }
}
```

## Configuration

The section has three knobs: `Enabled`, `FailOnConflict`, and `Attributes`.

```json
{
  "Temporal": {
    "SearchAttributes": {
      "Enabled": true,
      "FailOnConflict": false,
      "Attributes": {
        "CustomerId": { "Type": "Keyword" },
        "Amount":     { "Type": "Double" },
        "Note":       { "Type": "Text" },
        "Priority":   { "Type": "Int" },
        "IsVip":      { "Type": "Bool" },
        "OrderedAt":  { "Type": "Datetime" },
        "Tags":       { "Type": "KeywordList" }
      }
    }
  }
}
```

- `Enabled` (default `true`) — turn off registration where an environment lacks
  operator permission.
- `FailOnConflict` (default `false`) — when an attribute already exists with a
  different type, fail startup (`true`) rather than log a warning (`false`).
- `Attributes` — name → `Type`, where `Type` is one of `Keyword`, `Text`, `Int`,
  `Double`, `Bool`, `Datetime`, or `KeywordList`.

Registration runs after the connection waiter so the server is reachable first.
It applies to the default namespace plus every `Temporal:Namespaces` entry, and
is **add-only and idempotent**: missing attributes are added, existing ones are
left alone, and declared attributes are never removed.

This only bootstraps the *keys*. Setting *values* is done by your workflow code
through the SDK, either at start via `WorkflowOptions.TypedSearchAttributes` (or
`ChildWorkflowOptions.TypedSearchAttributes`) or during execution via
`Workflow.UpsertTypedSearchAttributes(...)`.

## Full configuration

`ISearchAttributeOps` is the client-side facade over the SDK's operator service,
registered as a singleton (like `IWorkflowOps`). Its `EnsureAsync` is the
idempotent core the registrar delegates to; `ListAsync` and `RemoveAsync` are
pass-throughs for parity, and `RemoveAsync` is not used by bootstrap (removal is
deliberately kept out of the startup path):

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
