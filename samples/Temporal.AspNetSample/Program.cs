using Kogoshvili.Temporal.Hosting;
using Kogoshvili.Temporal.AspNetSample;
using Temporalio.Client;

var builder = WebApplication.CreateBuilder(args);

// One-line startup: client + hosted worker with auto-discovery of [Workflow]
// and [Activity] types in this assembly.
builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("aspnet-sample");

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { Service = "Temporal.AspNetSample", TaskQueue = "aspnet-sample" }));

app.MapPost("/start/{name}", async (string name, ITemporalClient client) =>
{
    var handle = await client.StartWorkflowAsync(
        (GreetingWorkflow workflow) => workflow.RunAsync(name),
        new() { Id = $"greeting-{Guid.NewGuid():N}", TaskQueue = "aspnet-sample" });

    var greeting = await handle.GetResultAsync();
    return Results.Ok(new { WorkflowId = handle.Id, Greeting = greeting });
});

app.Run();
