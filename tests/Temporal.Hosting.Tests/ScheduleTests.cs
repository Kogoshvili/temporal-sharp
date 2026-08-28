using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Temporalio.Client.Schedules;
using Temporalio.Api.Enums.V1;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class ScheduleFactoryTests
{
    [Fact]
    public void BuildSchedule_MapsAllFields()
    {
        var config = new TemporalScheduleOptions
        {
            Action = new TemporalScheduleActionOptions
            {
                Workflow = "NightlyCleanupWorkflow",
                TaskQueue = "cleanup",
                WorkflowId = "{Type:s}-cleanup",
                RunTimeout = TimeSpan.FromMinutes(5),
                TaskTimeout = TimeSpan.FromSeconds(30),
                ExecutionTimeout = TimeSpan.FromHours(1),
                Retry = new RetryPolicyOptions { MaximumAttempts = 3 },
                StaticSummary = "summary",
                StaticDetails = "details",
            },
            Spec = new TemporalScheduleSpecOptions
            {
                Cron = new List<string> { "0 0 * * *" },
                TimeZoneName = "UTC",
                StartAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            Policy = new TemporalSchedulePolicyOptions
            {
                Overlap = ScheduleOverlapPolicy.BufferAll,
                CatchupWindow = TimeSpan.FromHours(2),
                PauseOnFailure = true,
            },
            State = new TemporalScheduleStateOptions
            {
                Note = "nightly",
                Paused = true,
            },
            TriggerImmediately = true,
            Reconcile = true,
        };

        var schedule = ScheduleFactory.BuildSchedule(config, "cleanup-schedule");

        var action = Assert.IsType<ScheduleActionStartWorkflow>(schedule.Action);
        Assert.Equal("NightlyCleanupWorkflow", action.Workflow);
        Assert.Equal("NightlyCleanup-cleanup", action.Options.Id);
        Assert.Equal("cleanup", action.Options.TaskQueue);
        Assert.Equal(TimeSpan.FromMinutes(5), action.Options.RunTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), action.Options.TaskTimeout);
        Assert.Equal(TimeSpan.FromHours(1), action.Options.ExecutionTimeout);
        Assert.Equal(3, action.Options.RetryPolicy!.MaximumAttempts);
        Assert.Equal("summary", action.Options.StaticSummary);
        Assert.Equal("details", action.Options.StaticDetails);

        Assert.Equal(new[] { "0 0 * * *" }, schedule.Spec.CronExpressions);
        Assert.Equal("UTC", schedule.Spec.TimeZoneName);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), schedule.Spec.StartAt);

        Assert.Equal(ScheduleOverlapPolicy.BufferAll, schedule.Policy.Overlap);
        Assert.Equal(TimeSpan.FromHours(2), schedule.Policy.CatchupWindow);
        Assert.True(schedule.Policy.PauseOnFailure);

        Assert.Equal("nightly", schedule.State.Note);
        Assert.True(schedule.State.Paused);
    }

    [Fact]
    public void BuildSchedule_UsesSdkDefaults_WhenSectionsOmitted()
    {
        var config = new TemporalScheduleOptions
        {
            Action = new TemporalScheduleActionOptions
            {
                Workflow = "Wf",
                TaskQueue = "q",
                WorkflowId = "wf-id",
            },
        };

        var schedule = ScheduleFactory.BuildSchedule(config, "id");

        Assert.Empty(schedule.Spec.Calendars);
        Assert.Empty(schedule.Spec.Intervals);
        Assert.Equal(ScheduleOverlapPolicy.Skip, schedule.Policy.Overlap);
        Assert.Equal(TimeSpan.FromDays(365), schedule.Policy.CatchupWindow);
        Assert.False(schedule.Policy.PauseOnFailure);
        Assert.False(schedule.State.Paused);
    }

    [Fact]
    public void BuildSchedule_MissingAction_Throws()
    {
        var config = new TemporalScheduleOptions();

        Assert.Throws<ArgumentException>(() => ScheduleFactory.BuildSchedule(config, "id"));
    }

    [Fact]
    public void BuildSchedule_MissingWorkflowId_Throws()
    {
        var config = new TemporalScheduleOptions
        {
            Action = new TemporalScheduleActionOptions { Workflow = "Wf", TaskQueue = "q" },
        };

        Assert.Throws<ArgumentException>(() => ScheduleFactory.BuildSchedule(config, "id"));
    }

    [Fact]
    public void BuildSchedule_IntervalMissingEvery_Throws()
    {
        var config = new TemporalScheduleOptions
        {
            Action = new TemporalScheduleActionOptions { Workflow = "Wf", TaskQueue = "q", WorkflowId = "id" },
            Spec = new TemporalScheduleSpecOptions
            {
                Intervals = new List<TemporalScheduleIntervalOptions> { new() },
            },
        };

        Assert.Throws<ArgumentException>(() => ScheduleFactory.BuildSchedule(config, "id"));
    }

    [Fact]
    public void BuildSchedule_CalendarRanges_MapToStructuredSpec()
    {
        var config = new TemporalScheduleOptions
        {
            Action = new TemporalScheduleActionOptions { Workflow = "Wf", TaskQueue = "q", WorkflowId = "id" },
            Spec = new TemporalScheduleSpecOptions
            {
                Calendars = new List<TemporalScheduleCalendarOptions>
                {
                    new()
                    {
                        Hour = new List<ScheduleRangeOptions> { new() { Start = 3 } },
                        DayOfWeek = new List<ScheduleRangeOptions> { new() { Start = 0, End = 4, Step = 2 } },
                        Comment = "weekdays",
                    },
                },
            },
        };

        var schedule = ScheduleFactory.BuildSchedule(config, "id");

        var calendar = Assert.Single(schedule.Spec.Calendars);
        Assert.Equal("weekdays", calendar.Comment);
        Assert.Equal(new ScheduleRange(3), Assert.Single(calendar.Hour));
        Assert.Equal(new ScheduleRange(0, 4, 2), Assert.Single(calendar.DayOfWeek));
        Assert.Equal(ScheduleCalendarSpec.Beginning, calendar.Minute);
    }
}

public class ScheduleRegistrationTests
{
    [Fact]
    public void AddTemporal_RegistersScheduleOps()
    {
        var services = new ServiceCollection();
        services.AddTemporal();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IScheduleOps>());
        Assert.IsType<ScheduleOps>(provider.GetService<IScheduleOps>());
    }

    [Fact]
    public void AddTemporal_RegistersScheduleRegistrar()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTemporal();

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(TemporalScheduleRegistrar));
    }

    [Fact]
    public void AddTemporalSchedule_AddsRegistration()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddTemporal();
        builder.AddTemporalSchedule(
            "my-schedule",
            new Schedule(
                new ScheduleActionStartWorkflow("Wf", Array.Empty<object?>(), new Temporalio.Client.WorkflowOptions("id", "q")),
                new ScheduleSpec()),
            reconcile: true);

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetServices<TemporalScheduleRegistration>();

        var registration = Assert.Single(registrations);
        Assert.Equal("my-schedule", registration.ScheduleId);
        Assert.True(registration.Reconcile);
    }

    [Fact]
    public void AddTemporal_Configuration_BindsSchedules()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
                ["Temporal:Schedules:nightly:Action:Workflow"] = "NightlyCleanupWorkflow",
                ["Temporal:Schedules:nightly:Action:TaskQueue"] = "cleanup",
                ["Temporal:Schedules:nightly:Action:WorkflowId"] = "nightly",
                ["Temporal:Schedules:nightly:Spec:Cron:0"] = "0 0 * * *",
                ["Temporal:Schedules:nightly:Policy:Overlap"] = "BufferAll",
                ["Temporal:Schedules:nightly:Reconcile"] = "true",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TemporalOptions>>().Value;

        var schedule = Assert.Single(options.Schedules!);
        Assert.Equal("nightly", schedule.Key);
        Assert.Equal("NightlyCleanupWorkflow", schedule.Value.Action!.Workflow);
        Assert.Equal("cleanup", schedule.Value.Action!.TaskQueue);
        Assert.Equal("nightly", schedule.Value.Action!.WorkflowId);
        Assert.Equal(new[] { "0 0 * * *" }, schedule.Value.Spec!.Cron);
        Assert.Equal(ScheduleOverlapPolicy.BufferAll, schedule.Value.Policy!.Overlap);
        Assert.True(schedule.Value.Reconcile);
    }
}
