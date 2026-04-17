# Ticket System (Concept)

This project is a concept for a modern ticket-system, a modern ticket management system.

## Project Structure (Monorepo)

- `src/frontend`: Nuxt 3 (Vue) web application for agents and clients.
- `src/backend`: ASP.NET Core API server with SignalR.
- `packages/`: Shared logic, types, and configurations.
- `docker/`: Docker configurations for infrastructure.

## Roadmap

### Phase 1: MVP
- JWT Based Authorization
- Tickets CRUD
- Clients Management
- Dashboard

### Phase 2: Communication
- Email Integration
- Comments
- Notifications
- Client Portal

### Phase 3: Automation
- SLA Control
- Auto-assignment
- Response Templates
- Analytics

- **Frontend**: Vue 3 / Nuxt
- **Backend**: C# / ASP.NET Core (SignalR)
- **Integrations**: Email, Telegram
- **Database**: PostgreSQL, Redis
- **Storage**: S3 / Minio
