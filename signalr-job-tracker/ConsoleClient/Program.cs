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

// Exercise: unlike the browser client, this one does NOT re-join groups after reconnect.
// Watch what happens to [update] lines after DropMe, then fix it here.
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

    var job = await connection.InvokeAsync<JobDto>("StartJob", line);
    await connection.InvokeAsync<JobDto?>("Watch", job.Id);
}

await connection.DisposeAsync();

record JobDto(Guid Id, string Name, string Status, int Progress);
