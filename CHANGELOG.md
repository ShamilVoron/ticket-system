# Changelog

## [0.1.0-beta.1] — 2026-07-27

Beta release of the self-hosted helpdesk / field-service platform (Nuxt 3 + ASP.NET Core 9 + PostgreSQL).

**Release tag (when cutting the release):** `v0.1.0-beta.1`  
*(Do not assume the tag exists until it is created explicitly.)*

### Highlights by phase

- **Core platform** — Auth (JWT + bcrypt), employees/staff accounts, tickets CRUD, comments, attachments with magic-byte validation, departments, companies/clients/objects, SignalR notifications.
- **Desk UX** — Ticket list/filters, new ticket + coordinator brief, status workflow, agent preferences, spreadsheets helper, reports.
- **SLA & Telegram** — Configurable SLA policies, background SLA monitor, Telegram bot event templates.
- **Okdesk & sync** — Okdesk import/test connection, outbound issue sync, staff API keys, optional Google Sheets company/object sync.
- **Messenger** — Internal staff messenger (direct/group), chat hub, attachment uploads, write rate limiting.
- **Field & portal** — `/field` engineer UI (list, detail, photos, visit reports), client portal paths, branding/onboarding settings.
- **Email & automation** — IMAP ingest hosted service, automation rules engine + CRUD.
- **Knowledge Base** — Categories/articles, published list/search/suggest (public read).
- **Hardening (Phase 8)** — Expanded integration tests (companies CRUD, messenger conversations, departments, KB published, `/health`), security checklist, deploy/admin/field docs; RabbitMQ health check only when `RabbitMQ:Enabled=true`.

### Ops notes

- Set `JWT_SECRET` (≥32 chars) before running the API.
- Hybrid Windows workflow and full Docker compose documented in `docs/deploy.md`.
- `/health` returns unhealthy (typically 503) if Rabbit is **enabled** but unreachable; with Rabbit disabled, DB-only health is enough.

### Docs added

- `docs/deploy.md`
- `docs/admin-guide.md`
- `docs/field-engineer-guide.md`
- `docs/security-checklist.md`
