# Koot

A Kahoot-like real-time quiz game.

## Stack

- **Backend:** ASP.NET Core (.NET 10) Web API + SignalR, EF Core 9 (Pomelo MySQL), JWT bearer auth, AutoMapper, Serilog.
- **Frontend:** Vue 3 + Vite + TypeScript, Tailwind CSS, Pinia, Vue Router, Axios, @microsoft/signalr.
- **Database:** MariaDB 11.
- **Orchestration:** docker-compose.

## Layout

```
backend/     .NET solution + Koot.Api project
frontend/    Vue 3 + Vite SPA
docker-compose.yml
```

## Running locally with Docker

```bash
docker compose up --build
```

Then visit:

- Frontend: http://localhost:5173
- Backend API: http://localhost:5024/api/health
- MariaDB: localhost:3306 (user `koot` / pw `koot`)

## Running pieces independently

### Backend

```bash
cd backend
dotnet run --project Koot.Api/Koot.Api.csproj
```

### Frontend

```bash
cd frontend
npm install
npm run dev
```

## Routes (placeholders, filled in by later tasks)

`/`, `/login`, `/register`, `/dashboard`, `/quiz/create`,
`/quiz/:id/edit`, `/host/:code`, `/join`, `/play/:code`.

## Tasks

Tracked on the CodeBoard project `KOOT`.
