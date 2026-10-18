# How SignalR works (using this project as the example)

Read it with the code open. Every section points to the file where the concept lives.

Sources: official Microsoft documentation (learn.microsoft.com/aspnet/core/signalr) and the protocol specification in the `dotnet/aspnetcore` repo (`src/SignalR/docs/specs/HubProtocol.md`). Links at the end.

## 1. What problem it solves

HTTP is request -> response. The server only answers when the client asks. For the browser to learn that a job reached 70%, it has to ask (polling), or the server needs a channel to send the message on its own.

SignalR provides that channel plus an RPC layer on top of it:

- the client calls a server method by name (`invoke("StartJob", "report")`),
- the server calls a "method" on the client by name (`Clients.All.JobUpdated(job)` -> `connection.on("JobUpdated", ...)`).

You do not write message parsing, routing, pings or transport-level reconnects. The library does that.

## 2. Mental model: the hub

```text
           client -> server: public hub methods
     +-------------------------------------------------+
     |                                                 v
 [JS / .NET client]                               [JobHub]
     ^                                                 |
     +-------------------------------------------------+
           server -> client: methods from IJobClient
```

A hub is a class that looks like a controller but works in both directions.

- `JobHub.StartJob`, `GetJobs`, `DropMe` - the client calls these on the server (`Server/JobHub.cs`).
- `IJobClient.JobStarted`, `JobUpdated` - the server calls these on the client. The interface has no C# implementation. SignalR turns the call into a message with `target: "JobUpdated"`, and the client handles it in `connection.on("JobUpdated", ...)`.

The method name is the whole contract. A typo in `connection.on("JobUpdate", ...)` gives no error, just silence. It is the most common beginner mistake.

### Strongly typed hub vs strings

This project uses `Hub<IJobClient>`, so you write `Clients.All.JobStarted(job)`. Without the interface it would be `Clients.All.SendAsync("JobStarted", job)`. Same result on the wire; the only difference is whether the compiler catches a typo on the server side.

### invoke vs send

- `invoke` / `InvokeAsync` - waits for the result or an exception from the server (`StartJob` returns a `Job`).
- `send` / `SendAsync` - fire-and-forget, does not wait (`drop` in the console client).

An exception thrown in a hub method reaches the client as an `invoke` error. By default without details (security), unless you throw `HubException` or enable `EnableDetailedErrors`.

## 3. Connection: negotiate and transport

When the client calls `connection.start()`:

```text
1. POST /hubs/jobs/negotiate        -> server returns connectionId, connectionToken and available transports
2. transport selection, in order:   WebSockets -> Server-Sent Events -> Long Polling
3. handshake                         -> {"protocol":"json","version":1}
4. from now on messages flow both ways
```

The transport is just a pipe. Application code is the same for each one (experiment 7 in the README). If a proxy or firewall blocks WebSockets, SignalR falls back to SSE or long polling by itself.

`ConnectionId` identifies one physical connection, not a user. New tab = new `ConnectionId`. Reconnect = new `ConnectionId` (exception: stateful reconnect, section 8).

## 4. Protocol: what goes over the wire

The default protocol is JSON. Every message ends with the `0x1E` character (record separator). The `type` field says what kind of message it is. In this project you will see:

| type | Name | When in this project |
|---|---|---|
| 1 | Invocation | client calls `StartJob` or server calls `JobUpdated` |
| 3 | Completion | server answers an `invoke` (result or error) |
| 6 | Ping | keep-alive, by default every 15 s from each side |
| 7 | Close | server closes the connection, may include `allowReconnect` |

Example from DevTools:

```json
{"type":1,"target":"JobUpdated","arguments":[{"id":"...","name":"report","status":"Running","progress":40}]}
```

Note `progress`, not `Progress`. The server serializes in camelCase, which is why JS uses `job.progress`. The .NET client maps names case-insensitively, so `JobDto` in `ConsoleClient` works.

Pings matter in practice. If the server receives nothing from a client for 30 s (`ClientTimeoutInterval`), it considers the connection dead. Same for the client towards the server (`serverTimeoutInMilliseconds`, 30 s by default).

## 5. Hub lifetime and IHubContext

A hub instance lives **only for one method call**. Every `invoke` creates a new `JobHub`, which is disposed when the method returns. Consequences:

- no state in hub fields (that is why jobs live in the `JobStore` singleton),
- do not use `Clients` from the hub after the method returns, e.g. in a `Task.Run` started from a hub method - the object is already disposed.

To send from outside a hub, use `IHubContext<JobHub, IJobClient>` from DI (`Server/JobStore.cs`). It works in a singleton, a worker (`BackgroundService`), a controller, a minimal API endpoint. `POST /jobs` in `Program.cs` shows it in practice: nobody touches the hub, yet clients receive messages.

`IHubContext` has no `Caller` or `Context`. Outside a hub there is no "current connection".

## 6. Who to send to

| Call | Who receives | In this project |
|---|---|---|
| `Clients.All` | everyone connected | `JobStarted`, `JobUpdated` |
| `Clients.Caller` | only the caller (hub only) | - |
| `Clients.Others` | everyone except the caller (hub only) | experiment 10 |
| `Clients.Client(connectionId)` | one connection | - |
| `Clients.Group(name)` | group members | not used, README experiment 10 |
| `Clients.User(userId)` | all connections of an authenticated user | requires authentication, out of scope |

## 7. Groups

A group is a named list of `ConnectionId`s. This project does not use groups (every client gets every job via `Clients.All`); a natural use would be group name = job id, so only clients watching a job get its progress (README experiment 10).

Facts you need to know:

- The first `AddToGroupAsync` creates the group. There is no separate "create group".
- **You cannot list the members of a group** or list the groups. If you need that, track it yourself.
- On disconnect a connection is removed from all groups automatically.
- **Groups do not survive a reconnect**, because a reconnect gets a new `ConnectionId`. The client has to rejoin (section 8).
- Groups live in the memory of a single server. With several instances you need a backplane (Redis) or Azure SignalR Service. Without it `Clients.Group` only sees connections on its own instance.

## 8. Reconnect and state restore

The most important section for real applications.

### What `withAutomaticReconnect()` does

- Without it, after losing the connection the client goes to `Disconnected` and that is it.
- With it: state `Reconnecting`, retries after 0, 2, 10, 30 s, then `Disconnected` and `onclose`. Custom delays: `.withAutomaticReconnect([0, 1000, 5000])`.
- It only works after a successful first `start()`. If the server is down at startup, `start()` throws and you have to handle that yourself.

### What it does NOT do

- It does not keep the `ConnectionId`.
- It does not restore group membership.
- **It does not deliver messages sent during the outage.** They are lost.

That is why `index.html` has `resync()` called in `onreconnected`:

```js
async function resync() {
  for (const job of await connection.invoke("GetJobs")) render(job); // current state of everything I missed
}
```

`render` is idempotent, so getting the same value from `GetJobs` and from a `JobUpdated` arriving right after does no harm.

If you use groups, the order matters: **join the group first, then read the state**. The other way around leaves a gap: an update sent between reading and joining is lost.

This pattern (state on the server + "fetch (and rejoin) after reconnect") is the standard one. SignalR is a notification channel, not the source of truth.

### Kick vs failure

- Dropped TCP / WebSocket (network, `HttpContext.Abort()`) - the client got no Close message, so it tries to come back.
- `Context.Abort()` in a hub - the server sends Close with `allowReconnect: false`. The client does not come back and goes straight to `onclose`. It is the tool for deliberately kicking a client. Confirmed in the source: `HubConnectionContext.Abort()` sets `_allowReconnect = false`.

`DropMe` in this project uses `HttpContext.Abort()` to imitate a network failure. Experiment 6 in the README shows the difference.

### Stateful reconnect (.NET 8+)

Optional mode: `AllowStatefulReconnects = true` in `MapHub` and `.withStatefulReconnect()` on the client. Server and client buffer unacknowledged messages (up to 100,000 bytes by default), and after a short drop the connection comes back **with the same `ConnectionId`, groups and pending messages**.

Limitation: it only helps with a short outage while the server process is still alive. A server restart or deploy is not covered. So the resync above is still needed; stateful reconnect only reduces how often it runs.

## 9. What SignalR does not give you

- Delivery guarantees or a queue. Client offline = message lost. Use a broker (RabbitMQ, Kafka) or state in a database for that.
- Persistence. Server restart = zero connections, zero groups.
- Horizontal scaling without a backplane.
- Per-message authorization. There is `[Authorize]` on the hub and its methods; the rest is up to you. Every hub method is a public endpoint: validate arguments like in a controller (`JobStore.Start` trims the name, `index.html` inserts it with `textContent`, not `innerHTML`, because names come from other users).

## 10. Deliberate shortcuts in this project

- Jobs in an in-memory `ConcurrentDictionary`, started fire-and-forget. In production: `BackgroundService` + `Channel` or a queue, state in a database.
- No authentication, so no `Clients.User`.
- JS client from a CDN instead of npm/LibMan.

## Links (official)

- Overview: https://learn.microsoft.com/aspnet/core/signalr/introduction
- Hubs: https://learn.microsoft.com/aspnet/core/signalr/hubs
- `IHubContext`: https://learn.microsoft.com/aspnet/core/signalr/hubcontext
- Groups and users: https://learn.microsoft.com/aspnet/core/signalr/groups
- JS client (reconnect): https://learn.microsoft.com/aspnet/core/signalr/javascript-client
- .NET client: https://learn.microsoft.com/aspnet/core/signalr/dotnet-client
- Configuration (timeouts, stateful reconnect): https://learn.microsoft.com/aspnet/core/signalr/configuration
- Streaming: https://learn.microsoft.com/aspnet/core/signalr/streaming
- Protocol specification: https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/docs/specs/HubProtocol.md
