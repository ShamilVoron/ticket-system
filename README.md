# Ticket System

Self-hosted helpdesk / field-service platform (Nuxt 3 + ASP.NET Core 9 + PostgreSQL).

## Stack

| Layer | Tech |
|-------|------|
| Frontend | Nuxt 3 (SPA), Vue 3, Pinia, Tailwind, SignalR |
| Backend | ASP.NET Core 9, EF Core, SignalR, FluentValidation |
| Database | PostgreSQL 15 |
| Optional infra | Redis, MongoDB, RabbitMQ (Docker) |

## Quick start (hybrid — recommended on Windows)

### 1. Prerequisites

- Node.js 20+
- .NET 9 SDK
- PostgreSQL on `localhost:5444` (or Docker below)

### 2. Environment

```powershell
# Copy example and set secrets
copy .env.example .env

$env:JWT_SECRET = "dev-secret-minimum-32-characters-long!"
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

Local Development DB defaults live in `src/backend/ITCafe.Api/appsettings.Development.json`.

### 3. Database

```powershell
cd src/backend/ITCafe.Api
dotnet ef database update
```

Migrations are under `src/backend/ITCafe.Api/Data/Migrations/`.

### 4. Backend

```powershell
cd src/backend/ITCafe.Api
$env:JWT_SECRET = "dev-secret-minimum-32-characters-long!"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --urls "http://localhost:5000"
```

Swagger: http://localhost:5000/swagger  
Default super-admin (Development): `admin@local.dev` / `admin123`

### 5. Frontend

```powershell
cd src/frontend
npm ci
npm run dev
```

App: http://localhost:3000

## Full Docker stack

```powershell
docker volume create tickets_postgres_data
$env:JWT_SECRET = "dev-secret-minimum-32-characters-long!"
$env:DB_PASSWORD = "change_me"
docker compose -f docker/docker-compose.yml up -d --build
```

- Frontend: http://localhost:3000  
- Backend: http://localhost:5000  
- Postgres host port: 5444  

## Solution / restore

There is no `.sln` file. Restore and build via project files:

```powershell
dotnet restore src/backend/ITCafe.Api/ITCafe.Api.csproj
dotnet build src/backend/ITCafe.Api/ITCafe.Api.csproj -c Release
dotnet test src/backend/ITCafe.Tests/ITCafe.Tests.csproj -c Release
```

## Project layout

- `src/frontend` — Nuxt SPA
- `src/backend/ITCafe.Api` — Web API
- `src/backend/ITCafe.Tests` — Integration tests
- `docker/` — Compose + helper script

## Roadmap (Beta 0.1)

See plan: Okdesk-style field service + Bitrix-lite internal messenger, field-engineer web UI, client portal, email ingest, automation, KB.

## Docs

- [Deploy](docs/deploy.md) — hybrid + Docker, migrations, JWT, health
- [Admin guide](docs/admin-guide.md) — statuses, SLA, Telegram, IMAP, Okdesk, automation, KB, branding
- [Field engineer](docs/field-engineer-guide.md) — `/field` UI
- [Security checklist](docs/security-checklist.md)
- [Changelog](CHANGELOG.md) — Beta `0.1.0-beta.1` (tag `v0.1.0-beta.1`)
