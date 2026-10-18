using Microsoft.AspNetCore.SignalR;

// Methods the SERVER calls on CLIENTS. Method name = event name the client listens to
// (connection.on("JobUpdated", ...) in JS, connection.On("JobUpdated", ...) in .NET).
public interface IJobClient
{
    Task JobStarted(Job job);
    Task JobUpdated(Job job);
}

// Public methods of the hub = methods CLIENTS call on the SERVER (connection.invoke("StartJob", ...)).
// A hub instance lives only for one call. Do not keep state in fields, do not use Clients after the method returns.
public class JobHub(JobStore jobs, ILogger<JobHub> logger) : Hub<IJobClient>
{
    public Job StartJob(string name) => jobs.Start(name);

    // Current state of all jobs, so a (re)connected client can catch up on what it missed.
    public IEnumerable<Job> GetJobs() => jobs.All();

    // Experiment helper: simulates a network drop by killing the TCP/WebSocket under SignalR, so automatic reconnect kicks in.
    // Context.Abort() would NOT do that: it sends "close, do not reconnect" = kicking the client out on purpose.
    public void DropMe() => Context.GetHttpContext()?.Abort();

    public override Task OnConnectedAsync()
    {
        logger.LogInformation("Connected {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // After reconnect the client has a NEW ConnectionId. Messages sent while it was gone are lost.
        logger.LogInformation("Disconnected {ConnectionId} ({Reason})", Context.ConnectionId, exception?.Message ?? "clean close");
        return base.OnDisconnectedAsync(exception);
    }
}
