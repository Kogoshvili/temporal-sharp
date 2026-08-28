using Kogoshvili.Temporal.Hosting;
using Temporalio.Exceptions;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class SagaTests
{
    [Fact]
    public async Task CompensateAsync_RunsInReverseOrder()
    {
        var saga = new Saga();
        var order = new List<string>();

        saga.AddCompensation(() => { order.Add("first"); return Task.CompletedTask; });
        saga.AddCompensation(() => { order.Add("second"); return Task.CompletedTask; });
        saga.AddCompensation(() => { order.Add("third"); return Task.CompletedTask; });

        await saga.CompensateAsync();

        Assert.Equal(new[] { "third", "second", "first" }, order);
    }

    [Fact]
    public async Task CompensateAsync_StopsAndRethrowsOnFirstFailure_ByDefault()
    {
        var saga = new Saga();
        var ran = new List<string>();

        saga.AddCompensation(() => { ran.Add("a"); return Task.CompletedTask; });
        saga.AddCompensation(() => throw new InvalidOperationException("boom"));
        saga.AddCompensation(() => { ran.Add("b"); return Task.CompletedTask; });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => saga.CompensateAsync());

        Assert.Equal("boom", ex.Message);
        Assert.DoesNotContain("a", ran);
    }

    [Fact]
    public async Task CompensateAsync_ContinueWithError_SwallowsAndContinues()
    {
        var saga = new Saga(new SagaOptions { ContinueWithError = true });
        var ran = new List<string>();

        saga.AddCompensation(() => { ran.Add("a"); return Task.CompletedTask; });
        saga.AddCompensation(() => throw new InvalidOperationException("boom"));
        saga.AddCompensation(() => { ran.Add("b"); return Task.CompletedTask; });

        await saga.CompensateAsync();

        Assert.Equal(new[] { "b", "a" }, ran);
    }

    [Fact]
    public async Task CompensateAsync_Parallel_RunsAllAndAggregatesFailures()
    {
        var saga = new Saga(new SagaOptions { ParallelCompensation = true });
        var ran = new List<string>();

        saga.AddCompensation(() => { ran.Add("a"); return Task.CompletedTask; });
        saga.AddCompensation(async () => { await Task.Yield(); throw new InvalidOperationException("one"); });
        saga.AddCompensation(async () => { await Task.Yield(); throw new InvalidOperationException("two"); });

        var ex = await Assert.ThrowsAsync<ApplicationFailureException>(() => saga.CompensateAsync());

        var aggregate = Assert.IsType<AggregateException>(ex.InnerException);
        Assert.Equal(2, aggregate.InnerExceptions.Count);
        Assert.Equal(new[] { "a" }, ran);
    }

    [Fact]
    public async Task CompensateAsync_Parallel_NoFailures()
    {
        var saga = new Saga(new SagaOptions { ParallelCompensation = true });
        var ran = new List<string>();

        saga.AddCompensation(() => { ran.Add("a"); return Task.CompletedTask; });
        saga.AddCompensation(() => { ran.Add("b"); return Task.CompletedTask; });

        await saga.CompensateAsync();

        Assert.Equal(new[] { "a", "b" }, ran);
    }

    [Fact]
    public async Task AddCompensation_ActionOverload_RunsSynchronously()
    {
        var saga = new Saga();
        var ran = 0;

        saga.AddCompensation(() => ran++);

        await saga.CompensateAsync();

        Assert.Equal(1, ran);
    }

    [Fact]
    public void AddCompensation_NullFunc_Throws()
    {
        var saga = new Saga();

        Assert.Throws<ArgumentNullException>(() => saga.AddCompensation((Func<Task>)null!));
    }

    [Fact]
    public void AddCompensation_NullAction_Throws()
    {
        var saga = new Saga();

        Assert.Throws<ArgumentNullException>(() => saga.AddCompensation((Action)null!));
    }
}
