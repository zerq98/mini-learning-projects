using Microsoft.AspNetCore.SignalR.Client;

var url = args.FirstOrDefault() ?? "http://localhost:5032/hubs/jobs";

var connection = new HubConnectionBuilder()
    .WithUrl(url)
    .WithAutomaticReconnect()
    .Build();

connection.On<JobDto>("JobStarted", job => Console.WriteLine($"[started] {job.Name}"));
connection.On<JobDto>("JobUpdated", job => Console.WriteLine($"[update]  {job.Name} {job.Status} {job.Progress}%"));

connection.Reconnecting += error =>
{
    Console.WriteLine($"[reconnecting] {error?.Message}");
    return Task.CompletedTask;
};

// Exercise: unlike the browser client, this one does NOT resync after reconnect.
// Updates sent during the outage are lost. Fix it here: print the current state from GetJobs.
connection.Reconnected += connectionId =>
{
    Console.WriteLine($"[reconnected] new ConnectionId {connectionId}");
    return Task.CompletedTask;
};

await connection.StartAsync();
Console.WriteLine($"Connected to {url} as {connection.ConnectionId}");
Console.WriteLine("Type a job name + Enter to start it, 'drop' to simulate a disconnect, empty line to quit.");

while (Console.ReadLine() is { Length: > 0 } line)
{
    if (line == "drop")
    {
        await connection.SendAsync("DropMe"); // SendAsync = fire-and-forget, InvokeAsync = waits for the result
        continue;
    }

    await connection.InvokeAsync<JobDto>("StartJob", line);
}

await connection.DisposeAsync();

record JobDto(Guid Id, string Name, string Status, int Progress);
