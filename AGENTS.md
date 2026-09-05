# AGENTS.md

This repository is the RGBOO Discord bot and the only persistent backend for the
RGBOO Board website.

## Invariants

- MongoDB is the source of truth. Discord commands and HTTP endpoints must use
  the same repository methods.
- The HTTP API is private to the website Worker. Require `Web__ApiKey` for every
  `/api/*` route, compare it in constant time, and never accept browser traffic
  directly.
- Resolve the guild and Discord member on every API request. Do not authorize a
  caller from an unverified user ID or from data hidden only in the frontend.
- Enforce private group visibility and `admin`, `organizer`, `member`, and
  `view_only` writes in the service layer.
- Model changes must be additive and safe for Mongo documents written by older
  releases. Keep legacy single-assignee/team fields working while the newer
  multi-person fields exist.
- Use friendly visible language such as cards, groups, people, notes, when, and
  importance. Internal class names may remain legacy names where renaming would
  jeopardize stored data compatibility.
- A stale `expectedVersion` returns `409 version_conflict`; never use last-write
  wins for website or Discord mutations.
- GitHub links accept only canonical HTTPS issue URLs. Do not fetch arbitrary
  user-provided URLs from the bot.
- Reuse one `MongoClient` for the process and preserve the startup indexes.
  Discord message IDs use a partial unique index and a guarded insert so retries
  cannot create duplicate cards.
- Defer Discord interactions before slow database work, keep personal responses
  private by default, and add a durable change-history entry for every card
  mutation.

## Structure

- `KanbanCord.Bot/Commands/`: Discord slash commands and interactions.
- `KanbanCord.Bot/Web/`: authenticated website contract and authorization.
- `KanbanCord.Core/Models/`: backward-compatible Mongo documents.
- `KanbanCord.Core/Repositories/`: all persistent reads and writes.
- `KanbanCord.Tests/`: Mongo-backed repository and compatibility tests.

## Required checks

Use the .NET SDK selected by `global.json`:

```bash
dotnet restore --locked-mode
dotnet format --no-restore --verify-no-changes
dotnet build --no-restore --configuration Release
dotnet test --no-restore --no-build --configuration Release
dotnet list KanbanCord.Bot/KanbanCord.Bot.csproj package --vulnerable --include-transitive --no-restore
dotnet list KanbanCord.Core/KanbanCord.Core.csproj package --vulnerable --include-transitive --no-restore
dotnet list KanbanCord.Tests/KanbanCord.Tests.csproj package --vulnerable --include-transitive --no-restore
docker build .
```

Treat warnings as errors. Add tests for compatibility, authorization boundaries,
version conflicts, validation, and destructive guards when those paths change.

## Delivery

CI runs on every pull request and `main` push. A green `main` deploys the Fly app
through a protected GitHub `production` Environment. Keep `/health` independent
of the authenticated board API so Fly and the website readiness check can use it.
Never put tokens or card contents in logs, source, workflow arguments, or images.
