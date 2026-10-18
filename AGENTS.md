# AGENTS.md

Rules for AI agents working in this repository. The repo is a collection of mini learning projects, one technology per project (see `README.md`).

## Language

- All text values and descriptions are in English: code, comments, UI strings, log messages, README files, docs, task logs, commit messages.
- Chat with the user may be in Polish; anything written into the repo is English.

## Creating a new project

- One technology per project, in its own top-level kebab-case folder (e.g. `signalr-job-tracker`).
- Scaffold with official CLI templates (`dotnet new`, `dotnet sln add`, `dotnet add package`), not hand-written project files.
- Use only official, trusted dependencies (NuGet packages published by the technology vendor, e.g. Microsoft). Prefer what the framework already ships over adding a package. Any non-NuGet dependency (e.g. a JS client from a CDN) must be the vendor's official package, pinned to an exact version, and listed in the project README.
- Look up the technology's API in current official documentation (context7 MCP) before writing code; do not rely on memory.
- Add a row for the new project to the table in the root `README.md`.
- Do not add a per-project `.gitignore`; the root one covers all projects.

## Shape of a project

- Turbo minimal: the smallest code that still shows the technology's key capabilities. No speculative abstractions, layers or config.
- Easy to experiment with: obvious entry points, small files, comments pointing at the concept each piece demonstrates.
- Deliberate shortcuts are allowed but must be named (code comment and a "deliberate shortcuts" section in the docs).
- Validate input at trust boundaries even in demos (e.g. user-provided text rendered to other users).

## Documentation per project

- `README.md`: what the project shows, how to start it, file -> concept map, reading order, numbered hands-on experiments (what to do -> what to observe -> a question to answer).
- `docs/`: how the technology works, explained on this project's code, with links to official sources. Separate confirmed facts from interpretation.
- Leave at least one exercise for the learner to implement, not just observe.

## Verification

- Build with no warnings.
- Run the project and check the key behaviours end to end before calling it done. Say plainly what was not verified (e.g. browser UI not clicked through).

## Task log

- Every working session ends with an entry in `<project>/.docs/__log__/` named `YYYY-MM-DD-<summary>.md`: title, JSON status block (`done` | `failed` | `done-with-clarification`), what changed, why.
