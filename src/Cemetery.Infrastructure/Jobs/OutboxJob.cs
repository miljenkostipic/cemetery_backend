using Cemetery.Application.Integrations;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cemetery.Infrastructure.Jobs;

public sealed class OutboxJob(IServiceScopeFactory scopes)
{
    public async Task DispatchAsync()
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchAsync(CancellationToken.None).ConfigureAwait(false);
    }
}

internal sealed class OutboxSchedule(IRecurringJobManager jobs) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        jobs.AddOrUpdate<OutboxJob>("outbox-dispatch", job => job.DispatchAsync(), Cron.Minutely);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
