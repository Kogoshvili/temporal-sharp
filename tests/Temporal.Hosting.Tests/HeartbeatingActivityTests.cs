using Temporalio.Converters;
using Temporalio.Testing;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class HeartbeatingActivityTests
{
    private record DownloadProgress(int PagesDone, long BytesDownloaded);

    private sealed class TestActivity : HeartbeatingActivity
    {
        public void DoHeartbeat(params object?[] details) => Heartbeat(details);

        public Task<T?> LoadAsync<T>() => LoadProgressAsync<T>();

        public IDisposable Start(TimeSpan? interval = null) => StartAutoHeartbeat(interval);
    }

    [Fact]
    public async Task Heartbeat_RelaysDetailsToContext()
    {
        var heartbeats = new List<object?[]>();
        var env = new ActivityEnvironment { Heartbeater = heartbeats.Add };
        var act = new TestActivity();

        await env.RunAsync(() =>
        {
            act.DoHeartbeat("a", 1);
            return "done";
        });

        var hb = Assert.Single(heartbeats);
        Assert.Equal(new object?[] { "a", 1 }, hb);
    }

    [Fact]
    public async Task LoadProgressAsync_NoDetails_ReturnsDefault()
    {
        var env = new ActivityEnvironment();
        var act = new TestActivity();

        var result = await env.RunAsync(() => act.LoadAsync<DownloadProgress>());

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadProgressAsync_WithDetails_ReturnsValue()
    {
        var progress = new DownloadProgress(42, 4096);
        var payload = DataConverter.Default.PayloadConverter.ToPayload(progress);
        var info = ActivityEnvironment.DefaultInfo with
        {
            HeartbeatDetails = new[] { payload },
        };
        var env = new ActivityEnvironment { Info = info };
        var act = new TestActivity();

        var result = await env.RunAsync(() => act.LoadAsync<DownloadProgress>());

        Assert.Equal(progress, result);
    }

    [Fact]
    public async Task StartAutoHeartbeat_RelaysLastDetails_NotEmpty()
    {
        var heartbeats = new List<object?[]>();
        var env = new ActivityEnvironment { Heartbeater = heartbeats.Add };
        var act = new TestActivity();
        var progress = new DownloadProgress(1, 100);

        await env.RunAsync(async () =>
        {
            act.DoHeartbeat(progress);
            using (act.Start(TimeSpan.FromMilliseconds(10)))
            {
                await Task.Delay(100);
            }
        });

        Assert.NotEmpty(heartbeats);
        Assert.All(heartbeats, hb => Assert.Equal(new object?[] { progress }, hb));
    }

    [Fact]
    public async Task StartAutoHeartbeat_NoProgressYet_SendsEmpty()
    {
        var heartbeats = new List<object?[]>();
        var env = new ActivityEnvironment { Heartbeater = heartbeats.Add };
        var act = new TestActivity();

        await env.RunAsync(async () =>
        {
            using (act.Start(TimeSpan.FromMilliseconds(10)))
            {
                await Task.Delay(100);
            }
        });

        Assert.NotEmpty(heartbeats);
        Assert.All(heartbeats, hb => Assert.Empty(hb));
    }

}
