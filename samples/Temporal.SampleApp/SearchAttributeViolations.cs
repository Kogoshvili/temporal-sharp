using Temporalio.Workflows;

namespace Kogoshvili.Temporal.SampleApp;

// TMP2161 (opt-in) — a workflow input field mapped to a search attribute but
// never upserted. The field->attribute mapping is supplied by
// `kogoshvili.temporal.search_attributes` in this sample's .editorconfig.

public class CustomerInput
{
    public string CustomerId { get; set; } = "";
}

// TMP2161 — CustomerId maps to a search attribute but is never upserted via
// Workflow.UpsertTypedSearchAttributes, so Temporal only indexes the value set
// at workflow start.
[Workflow]
public class SearchAttributeViolationWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(CustomerInput input)
    {
        await Task.CompletedTask;
    }
}
