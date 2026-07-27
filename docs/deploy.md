# Deploy guide

## Hybrid (recommended on Windows)

Run PostgreSQL (and optional Redis/Mongo/Rabbit) in Docker; run API and Nuxt on the host.

### Prerequisites

- Node.js 20+
- .NET 9 SDK
- PostgreSQL on `localhost:5444` (or start only the DB from compose)

### Environment

```powershell
copy .env.example .env

$env:JWT_SECRET = "dev-secret-minimum-32-characters-long!"
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

`JWT_SECRET` is **required**. Local DB defaults: `src/backend/ITCafe.Api/appsettings.Development.json`.

### Migrations

```powershell
cd src/backend/ITCafe.Api
dotnet ef database update
```

Migration sources: `src/backend/ITCafe.Api/Data/Migrations/`.

### Backend

```powershell
cd src/backend/ITCafe.Api
$env:JWT_SECRET = "dev-secret-minimum-32-characters-long!"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --urls "http://localhost:5000"
```

- Swagger (Development): http://localhost:5000/swagger  
- Default super-admin (Development seed): `admin@local.dev` / `admin123`

### Frontend

```powershell
cd src/frontend
npm ci
npm run dev
```

App: http://localhost:3000

---

## Full Docker stack

```powershell
docker volume create tickets_postgres_data
$env:JWT_SECRET = "dev-secret-minimum-32-characters-long!"
$env:DB_PASSWORD = "change_me"
docker compose -f docker/docker-compose.yml up -d --build
```

| Service   | URL / port        |
|-----------|-------------------|
| Frontend  | http://localhost:3000 |
| Backend   | http://localhost:5000 |
| Postgres  | host port **5444** |
| Rabbit MQ | 5673 (AMQP), 15673 (mgmt) |

Compose injects `JWT_SECRET` into the backend container (`:?` required). Set strong `DB_PASSWORD` / Rabbit credentials for anything beyond local play.

Default `appsettings.json` has `RabbitMQ:Enabled: false` (MassTransit in-memory). To use the Rabbit container, set `RabbitMQ__Enabled=true` (and host/credentials already present in compose).

---

## Health

- Endpoint: **`GET /health`**
- Always checks PostgreSQL (EF DbContext).
- RabbitMQ check is registered **only when** `RabbitMQ:Enabled=true`.
  - With Rabbit enabled and broker down → overall health **Unhealthy** (clients typically see **503**).
  - With Rabbit disabled → `/health` does not depend on Rabbit (API can be Healthy without the broker).

Docker compose healthcheck hits `http://127.0.0.1:5000/health`.

---

## Build / test (no solution file)

```powershell
dotnet restore src/backend/ITCafe.Api/ITCafe.Api.csproj
dotnet build src/backend/ITCafe.Api/ITCafe.Api.csproj -c Release
$env:JWT_SECRET = "test-jwt-secret-32-chars-long-min!!!"
dotnet test src/backend/ITCafe.Tests/ITCafe.Tests.csproj -c Release
```
