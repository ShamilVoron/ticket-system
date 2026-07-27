# Admin guide

Operator-facing overview of Settings and related modules. UI: **Settings** (`/settings`) for staff with admin rights; Knowledge Base and Automation have dedicated screens/APIs.

## Ticket statuses

- Configure under Settings → **Statuses**.
- Each status: name, color class, sort order, optional role filter, default/active flags.
- Default workflow names often include `Открыт`, `В работе`, `Ожидание`, `Решён`/`Решено`, `Закрыт` — customize to match your process.
- Staff comment on an `Открыт` ticket typically bumps status to `В работе`.

## SLA

- Settings → **SLA**: policies with reaction and resolution minutes.
- Matchers: priority, request type, department, client category — use `*` as wildcard.
- Most specific matching policy wins.
- Background monitor can fire Telegram events `sla_80` / `sla_breach` when configured.

## Telegram

- Settings → **Telegram**: bot/event rules (event type, chat id or employee target, template, enable flag).
- Useful events: status changes, assignment, SLA thresholds.
- Employees can store `TelegramChatId` for personal alerts when target type is employee.

## IMAP (email ingest)

- Settings key/value (and Settings UI): `email_ingest_enabled`, `imap_host`, `imap_port`, `imap_user`, `imap_password`, `imap_use_ssl`.
- Hosted service polls mailbox and creates tickets from inbound mail when enabled.
- Keep mailbox credentials in secrets; prefer app-specific passwords.

## Okdesk

- Settings → Okdesk: `OkdeskApiUrl`, `OkdeskApiToken`.
- Test connection, then **import** (super_admin) for companies / open issues.
- Outbound sync pushes status, assignee, priority, title/description, comments for tickets linked with Okdesk ids.
- Staff API key is useful for one-off migration scripts.

## Automation

- Rules via Automation API / Settings automation UI: name, active flag, **trigger**, JSON conditions and actions.
- Engine runs on a hosted service (ticket events / schedules depending on rule).
- Start with narrow conditions; log and dry-run mentally before broad `*` rules.

## Knowledge Base

- Categories and articles (staff CRUD).
- **Published** articles are publicly readable (`/api/KnowledgeBase/articles/published`, search, suggest).
- Use suggest from ticket title to surface related articles for agents and portal users.
- Draft (`IsPublished=false`) stays staff-only.

## Branding

- System settings: `company_name`, `brand_logo_url`, `brand_accent_color`.
- Applied in default and field layouts (logo, accent CSS variable).
- Logo URL should be reachable by browsers (often under `/uploads/...` or a CDN).

## Onboarding

- Flag `onboarding_completed` in system settings (consumed by `useSystemBranding`).
- After first-time company name / logo / admin setup, mark onboarding complete so prompts stop.
- Seeded Development super-admin: change password before sharing the environment.

## Staff API key & sync

- Generate a `ts_…` key bound to a staff user for integrations (see [security-checklist.md](./security-checklist.md)).
- Google Sheets company/object sync uses `Sync:ApiKey` — keep it out of the repo.
