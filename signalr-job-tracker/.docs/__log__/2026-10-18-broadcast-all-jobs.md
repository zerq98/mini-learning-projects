# 2026-10-18 - Broadcast every job to every client

```json
{
  "status": "done",
  "models": "claude-opus-5-5"
}
```

What changed:
- `JobStore.RunAsync` sends `JobUpdated` via `Clients.All` instead of `Clients.Group(jobId)`.
- Removed group machinery that became dead code: `JobHub.Watch` / `Unwatch`, `JobStore.Get`, the Watch column and `watched` set in `index.html`, the `Watch` call in `ConsoleClient`.
- Console client exercise reworded: after reconnect, resync with `GetJobs` (updates sent during the outage are lost).
- README (intro, file map, flow, experiments 1/5/8/10) and `docs/how-signalr-works.md` (sections 1, 2, 6, 7, 8) updated. Groups stay in the docs as theory and as a new "next steps" exercise.

Why: the user asked for all jobs to go to all connected clients.

Verification: build with no warnings; end-to-end with server + two console clients, job started via `POST /jobs`: both clients received `JobStarted` and all ten `JobUpdated` messages. Browser UI was not clicked through.
