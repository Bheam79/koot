# Koot — agent notes

Working knowledge for future dispatches. Keep short and observational.

## Stack pin-points

- Backend uses **EF Core 9 + Pomelo 9** even though the runtime is **.NET 10**.
  Pomelo doesn't have an EF Core 10 release yet (only PRs in flight). Keep the
  EF packages at `9.0.x`. The csproj will warn (NU1608) only if you bring
  EF Core 10 packages back in.
- `AutoMapper 14.0.0` is pinned. v15+ switched to a commercial license and a
  different DI API (`Action<IMapperConfigurationExpression>`); upgrading needs
  a Program.cs rewrite. v14 has an open NU1903 advisory we accept for now.
- `MariaDbServerVersion(11.4)` is hard-coded in `Program.cs` and
  `AppDbContextFactory` — using `ServerVersion.AutoDetect` would force the
  host to talk to MySQL at startup and during migration scaffolding.
- The frontend `tsconfig` enables `erasableSyntaxOnly`, so **TS `enum`
  declarations fail vue-tsc** (`TS1294`). Mirror backend enums with a
  `const` object + lookup type:
  ```ts
  export const QuestionType = { MultipleChoice: 0, /* ... */ } as const
  export type QuestionType = (typeof QuestionType)[keyof typeof QuestionType]
  ```

## EF Core migrations

```bash
cd backend/Koot.Api
dotnet ef migrations add <Name> --output-dir Data/Migrations
# Apply against a running DB:
ConnectionStrings__DefaultConnection="Server=...;Port=3306;Database=koot;User=koot;Password=koot;" \
  dotnet ef database update
```

`AppDbContextFactory` exists so `dotnet ef` doesn't need to build the full
web host or reach the DB to scaffold migrations.

## Local ephemeral DB for testing migrations

The host port `3306` is in use by something on the host; bind without `-p`
and use the container IP instead:

```bash
docker run -d --name koot-mariadb-test \
  -e MARIADB_ROOT_PASSWORD=rootpass -e MARIADB_DATABASE=koot \
  -e MARIADB_USER=koot -e MARIADB_PASSWORD=koot mariadb:11
DB_IP=$(docker inspect koot-mariadb-test --format '{{.NetworkSettings.IPAddress}}')
until docker exec koot-mariadb-test mariadb-admin ping -uroot -prootpass --silent; do sleep 1; done
ConnectionStrings__DefaultConnection="Server=$DB_IP;Port=3306;Database=koot;User=koot;Password=koot;" \
  dotnet ef database update
docker rm -f koot-mariadb-test
```

## GitHub remote

- Repo lives at `Bheam79/koot` (note: Bheam79 is a **user** account, not an
  org — the task description called it an org, but the org doesn't exist).
- The remote URL in `.git/config` is sanitised (no PAT). To push, pass the
  PAT inline:
  ```bash
  git push https://Bheam79:<PAT>@github.com/Bheam79/koot.git main
  ```
  The PAT was originally in KOOT-2's description; it's also in commit history.
  Treat it as compromised — recommend rotation in any future security task.

## Quick verification

- `curl http://localhost:5024/api/health` — backend liveness.
- `cd frontend && npm run build` — frontend SPA build.
- `cd backend && dotnet build` — backend build (2 NU1903 warnings = OK,
  errors = real problem).

## Frontend dev server on port 443 (KOOT-13)

Per the project lead, the Vite dev server runs **directly inside this dev
container** (not in a sibling docker container) bound to container port
`443`. The host has `31143 -> :443` pre-mapped, so the live URL
`http://koot.ai.ba.gl:31143/` reaches Vite directly.

- `/usr/bin/node` has `cap_net_bind_service=ep` set, so `claude` (uid 1000)
  can bind to port 443 without sudo or container restart.
- `frontend/vite.config.ts` pins `server.port=443`, `host='0.0.0.0'`, and
  `allowedHosts: ['koot.ai.ba.gl', 'localhost', '127.0.0.1']` (Vite blocks
  unknown Host headers by default).
- `docker-compose.yml` still maps the frontend service to host port `5173`;
  that's for users who prefer running the whole stack via compose. The
  in-container dev-server flow does not use docker-compose.
- Start it: `cd frontend && nohup npm run dev > /tmp/koot-dev.log 2>&1 &`.
  Verify with `curl -s -o /dev/null -w "%{http_code}\n" http://localhost:443/`.
