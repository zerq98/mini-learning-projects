using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

public record Job(Guid Id, string Name, string Status, int Progress);

// Singleton. Pushes to clients from OUTSIDE the hub via IHubContext - the hub instance is long gone by then.
public class JobStore(IHubContext<JobHub, IJobClient> hub)
{
    readonly ConcurrentDictionary<Guid, Job> _jobs = new();

    public IEnumerable<Job> All() => _jobs.Values;

    public Job Start(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "job" : name.Trim();
        var job = new Job(Guid.NewGuid(), name[..Math.Min(name.Length, 40)], "Queued", 0);
        _jobs[job.Id] = job;
        // ponytail: fire-and-forget, jobs live in memory and die with the process; BackgroundService + Channel if they must survive errors
        _ = RunAsync(job);
        return job;
    }

    async Task RunAsync(Job job)
    {
        await hub.Clients.All.JobStarted(job);

        for (var progress = 10; progress <= 100; progress += 10)
        {
            await Task.Delay(1000);
            job = job with { Status = progress < 100 ? "Running" : "Done", Progress = progress };
            _jobs[job.Id] = job;
            await hub.Clients.All.JobUpdated(job); // every connected client gets every job's progress
        }
    }
}
