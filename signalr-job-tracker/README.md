# SignalR Job Tracker

A mini project for learning ASP.NET Core SignalR (.NET 10). You start a short job (10 s) and its status is pushed live to every connected browser tab and console client (`Clients.All`).

Covered: hubs, client -> server and server -> client calls, broadcasting to all clients, sending from outside a hub (`IHubContext`), automatic reconnect and restoring state after a disconnect.

Theory (how it works and why): [docs/how-signalr-works.md](docs/how-signalr-works.md)

## Start (2 minutes)

```powershell
dotnet run --project Server          # http://localhost:5032
dotnet run --project ConsoleClient   # optional, in a second terminal
```

Open `http://localhost:5032` in **two tabs** side by side. Click **Start** in one of them.

## File map

| File | What is in it | SignalR concept |
|---|---|---|
| `Server/Program.cs` | SignalR registration, `MapHub`, `POST /jobs` endpoint | `AddSignalR`, `MapHub<T>` |
| `Server/JobHub.cs` | hub: methods called by clients + interface of client methods | `Hub<T>`, `Context`, `OnConnectedAsync` |
| `Server/JobStore.cs` | in-memory job state + "worker" that pushes progress | `IHubContext<THub, T>`, `Clients.All` |
| `Server/wwwroot/index.html` | JS client (official `@microsoft/signalr` from CDN) | `HubConnectionBuilder`, `on`, `invoke`, `withAutomaticReconnect` |
| `ConsoleClient/Program.cs` | .NET client (NuGet `Microsoft.AspNetCore.SignalR.Client`) | `On<T>`, `InvokeAsync`, `SendAsync`, `Reconnected` |

Reading order: `JobHub.cs` -> `JobStore.cs` -> `index.html` (the `<script>` section) -> `Program.cs` of both projects.

## Flow of a single job

```text
 Tab A                       Server                                  Tab B
 -----                       ------                                  -----
 invoke("StartJob") -------> JobHub.StartJob -> JobStore.Start
                             RunAsync: Clients.All.JobStarted -----> on("JobStarted")  (sees the row)
 on("JobStarted") <--------/
                             every 1 s: Clients.All.JobUpdated ----> on("JobUpdated")  (bar moves)
 on("JobUpdated") <--------/
```

## Experiments

Each one: what to do -> what to observe -> a question to answer yourself. Go in order, each takes 2-5 minutes.

### 1. Broadcast to everyone
1. Two tabs. Start a job in tab A.
2. Tab B gets the new row and its progress bar moves in sync with tab A.
3. Open a third tab while the job is running. It shows the job at its current progress right away.

Question: where did the third tab get the current progress from, if it never received the earlier `JobUpdated` messages? (answer: `resync()` calls `GetJobs` after `start()`, `index.html`)

### 2. Look at the frames on the wire
1. DevTools (F12) -> Network -> `WS` filter -> reload the page.
2. Click the `jobs?id=...` connection -> **Messages** tab.
3. Click Start.

You will see JSON like `{"type":1,"target":"JobUpdated","arguments":[...]}` terminated by the `0x1E` character, and `{"type":6}` (ping) every ~15 s. Before the WebSocket the Network tab also shows `POST /hubs/jobs/negotiate`. Message types are described in [docs/how-signalr-works.md](docs/how-signalr-works.md#4-protocol-what-goes-over-the-wire).

### 3. Push from outside the hub
```powershell
Invoke-RestMethod -Method Post "http://localhost:5032/jobs?name=from-terminal"
```
The row appears in every tab. Nobody called the hub; the server pushed the message from an HTTP endpoint through `IHubContext`.

Question: why can't `JobStore` simply keep a reference to `JobHub`?

### 4. Dropped connection and state restore
1. Start a job in tab A.
2. Immediately click **Drop my connection**.
3. Log shows `reconnecting` -> `reconnected, new ConnectionId ...`. The `ConnectionId` at the top of the page changes. The progress bar jumps.

Question: what would happen without the `resync()` function in `index.html`? Check it: comment out its call in `onreconnected`, reload, repeat.

### 5. Fix the console client (coding exercise)
1. Run `ConsoleClient`, type `report` + Enter. `[update]` lines appear.
2. Type `drop` + Enter while the job is running.
3. During the outage no `[update]` lines arrive. After `[reconnected]` they come back, but the progress values sent in between are missing (the browser shows them all).

Task: fix the `Reconnected` handler in `ConsoleClient/Program.cs` so it prints the current state of every job right after reconnecting. Hint: the hub already has a method for that.

### 6. Kick vs failure
In `JobHub.DropMe` replace `Context.GetHttpContext()?.Abort()` with `Context.Abort()`. Restart the server, click **Drop my connection**.

The log shows `closed` right away, with no `reconnecting`. `Context.Abort()` sends the client a Close message with `allowReconnect: false`, meaning "I am disconnecting you on purpose, do not come back". A dropped TCP connection carries no such message, so the client tries to come back.

### 7. Force a different transport
In `index.html`:
```js
.withUrl("/hubs/jobs", { transport: signalR.HttpTransportType.LongPolling })
```
Instead of one WebSocket the Network tab shows a series of `GET /hubs/jobs?id=...` requests, each hanging until the server has something to send. The application code does not change, and that is the main value SignalR adds over a raw WebSocket. Try `ServerSentEvents` too.

### 8. Server down for longer
1. Stop the server (Ctrl+C) and watch the log in the tab.
2. The client retries after 0, 2, 10 and 30 s, then `closed`.
3. Start the server again and reload the tab. The job list is empty.

Takeaway: SignalR keeps no application state. Jobs live in the process memory.

### 9. Server diagnostic logs
In `Server/appsettings.Development.json` add to `LogLevel`:
```json
"Microsoft.AspNetCore.SignalR": "Debug",
"Microsoft.AspNetCore.Http.Connections": "Debug"
```
You will see negotiation, the selected transport and every hub method call.

### 10. Next steps, on your own
In order, simplest first:
- `Clients.Others` - notify the others that someone started a job, without an echo to the author.
- Groups - bring back opt-in progress: hub methods `Watch(Guid id)` / `Unwatch(Guid id)` with `Groups.AddToGroupAsync`, and `Clients.Group(id).JobUpdated` in `JobStore`. Remember to rejoin after reconnect ([docs, section 7](docs/how-signalr-works.md#7-groups)).
- Hub method `CancelJob(Guid id)` - job cancellation (a `CancellationTokenSource` per job in `JobStore`).
- Streaming: a hub method returning `IAsyncEnumerable<int>` with progress; on the client `connection.stream(...)` / `StreamAsync<int>`.
- Stateful reconnect: `AllowStatefulReconnects = true` in `MapHub` + `.withStatefulReconnect()` on the client. Repeat experiment 4 and compare `ConnectionId` before and after.

## Dependencies

- Server: only `Microsoft.NET.Sdk.Web` (SignalR is part of the ASP.NET Core shared framework, no NuGet package needed).
- .NET client: `Microsoft.AspNetCore.SignalR.Client` 10.0.13 (Microsoft, nuget.org).
- JS client: `@microsoft/signalr` 10.0.0 from jsDelivr (Microsoft's official npm package). The only dependency outside NuGet. LibMan/npm were skipped to avoid extra tooling in a mini project.
