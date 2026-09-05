# RGBOO Discord Board bot

The Discord bot and private HTTP backend for the RGBOO Board. Discord commands
and the website share the same repository layer and MongoDB documents, so there
is no copied data or synchronization service.

This project started from
[KanbanCord](https://github.com/j4asper/KanbanCord) and retains its MIT license.

DSharpPlus 5 is still distributed as prerelease builds. This repository pins an
exact, tested build in the central package file and NuGet lockfiles instead of
floating to a newer nightly release automatically.

## Requirements

- .NET 10 LTS SDK (selected by `global.json`)
- a Discord application and bot
- MongoDB

Enable **Server Members Intent** in the Discord developer portal. It lets the
website list the small guild roster for group assignment. The bot needs the
`applications.commands` and `bot` installation scopes.

## Discord surface

- `/my-list` shows cards involving you.
- `/card add`, `open`, `update`, `move`, `people`, `note`, `checklist`, and
  `github` cover normal card work.
- `/card update` can change title, notes, tags, date, importance, and waiting
  state in one private command.
- `/board recap` summarizes a board.
- Right-click a message, choose **Apps → Add to The Board**, and the bot creates
  one linked card. A unique MongoDB index makes Discord retries safe.
- Card responses provide Join this, Move, Mark done, Waiting, Add note, and Open
  The Board actions.

Normal card responses are private unless a person deliberately requests a
shared recap.
Group membership, roles, themes, boards, and the five visible columns remain in
the website's organizer screens.

## Configuration

ASP.NET Core maps double underscores in environment variable names to nested
configuration.

| Variable                              | Purpose                                                                             |
| ------------------------------------- | ----------------------------------------------------------------------------------- |
| `Discord__Token`                      | Discord bot token                                                                   |
| `Database__ConnectionString`          | MongoDB connection string                                                           |
| `Database__Name`                      | Database name; defaults to `KanbanCord`                                             |
| `Web__ApiKey`                         | Service credential of at least 32 random bytes, shared only with the website Worker |
| `Web__AdministratorDiscordUserIds__0` | Bootstrap administrator Discord ID                                                  |
| `Web__PublicBoardUrl`                 | Deployed Cloudflare URL used by the bot's **Open The Board** button                 |
| `UptimeMonitor__Enabled`              | Enables the optional push heartbeat                                                 |
| `UptimeMonitor__PushUrl`              | External monitor heartbeat URL                                                      |
| `UptimeMonitor__PushInterval`         | Heartbeat interval, for example `00:05:00`                                          |

Add more administrator IDs with increasing array indexes. Store all secrets in
Fly or another runtime secret store, never in `appsettings.json`.

## Run and verify

```bash
dotnet restore --locked-mode
dotnet run --project KanbanCord.Bot
```

```bash
dotnet format --no-restore --verify-no-changes
dotnet build --no-restore --configuration Release
dotnet test --no-restore --no-build --configuration Release
dotnet list KanbanCord.Bot/KanbanCord.Bot.csproj package --vulnerable --include-transitive --no-restore
dotnet list KanbanCord.Core/KanbanCord.Core.csproj package --vulnerable --include-transitive --no-restore
dotnet list KanbanCord.Tests/KanbanCord.Tests.csproj package --vulnerable --include-transitive --no-restore
docker build .
```

The service listens on the configured ASP.NET URL. `/health` checks Discord and
MongoDB without requiring the website API key.

For a containerized local stack, copy `.env.example` to `.env`, fill in
`DISCORD_TOKEN`, `WEB_API_KEY`, and `ADMIN_DISCORD_USER_ID`, then run
`docker compose up --build`. This builds the checked-out source, starts MongoDB
8, waits for it to become healthy, and exposes the bot API at
`http://localhost:5000`. `PUBLIC_BOARD_URL` and `DATABASE_NAME` are optional.

## Website API

Every `/api/*` request requires `Authorization: Bearer <Web__ApiKey>`, plus the
trusted `X-Discord-Guild-Id` and `X-Discord-User-Id` headers added by the
Cloudflare Worker. The API re-fetches the Discord member and applies group roles
before reading or writing MongoDB.

Routes cover the dashboard, five customizable columns, card
creation/editing/ordering, comments, checklists, GitHub issue links, source
Discord links, durable change history, groups, per-group roles, and boards.
Website and Discord updates use optimistic versions instead of overwriting a
newer edit.

The process reuses one MongoDB connection pool. Startup creates named indexes
for board reads, group/board names, and Discord source-message deduplication.
Model and index changes remain compatible with older cards that lack the newer
fields.

## Deployment

`fly.toml` keeps one machine running, uses rolling deploys, and checks `/health`.
The GitHub `production` Environment needs a narrowly scoped `FLY_API_TOKEN`.
After adding that secret, set the repository Actions variable
`ENABLE_DEPLOYMENTS` to `true`. A passing `main` pipeline then publishes and
checks the public health endpoint. Until the variable is enabled, deploy jobs
remain safely skipped. Runtime application settings remain Fly secrets.

Console output is the only production log sink so Fly can retain and search it;
the non-root container does not write local log files.
