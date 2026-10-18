# 2026-10-18 - SignalR Job Tracker, mini learning project

```json
{
  "status": "done",
  "models": "claude-opus-5-5"
}
```

Created solution `SignalrJobTracker.slnx` with `dotnet new` (sln, web, console), .NET 10:
- `Server` - hub `JobHub` (Hub<IJobClient>), `JobStore` (in-memory state + push via IHubContext), `POST /jobs`, JS client in `wwwroot/index.html` (`@microsoft/signalr` 10.0.0 from jsDelivr).
- `ConsoleClient` - .NET client (`Microsoft.AspNetCore.SignalR.Client` 10.0.13); the `Reconnected` handler deliberately has no resync, left as an exercise.
- `README.md` (start, file map, 10 experiments), `docs/how-signalr-works.md` (theory based on this project).

Verification: build with no warnings; end-to-end test with a .NET client: push to group, no updates for a client outside the group, after a drop a new ConnectionId and lost group membership, `Watch` after reconnect returns current state; `POST /jobs` works. Browser UI was not clicked through automatically.

Topic taken from the vault `learning/technology-practice-list.md` (SignalR: hub, groups, reconnect, state after a disconnect). `DropMe` uses `HttpContext.Abort()` because `Context.Abort()` sends Close with allowReconnect=false and the client does not come back (confirmed by test and in the aspnetcore source).

Follow-up: all text and documentation translated to English; repo root got `README.md`, `AGENTS.md`, `.gitignore` and `git init`.
