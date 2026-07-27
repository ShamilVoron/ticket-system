# Security checklist (Beta)

Quick review notes for operators and reviewers. Not a formal audit.

## JWT required

- Most API controllers use `[Authorize]` (or role/policy variants such as `StaffOnly`).
- Configure a strong secret via `JWT_SECRET` (env) or `Jwt:Secret` (config). Startup **fails** if neither is set.
- Use at least 32 characters in production; rotate on compromise.
- SignalR hubs accept `access_token` query for WebSocket auth (standard browser limitation).

## Upload magic bytes

- Ticket attachments, chat uploads, and avatar uploads validate file content with `FileSignatureValidator` (JPEG/PNG/GIF/WEBP/PDF/Office ZIP/MP4 signatures).
- Extension alone is not trusted for known binary types; mismatch → rejected.
- Unknown extensions (e.g. `.txt`) skip signature checks — keep allow-lists tight on the client where possible.

## Messenger rate limit

- Global API limiter: policy `fixed` (120 req/min per partition).
- Messenger write endpoints (`POST` conversations / messages / uploads) use `messenger_write`: **45 writes/min per user** (JWT `sub` / NameIdentifier).
- Exceeding the limit returns **429**.

## Staff API key

- Long-lived key (`ts_…`) for scripts/integrations (Okdesk migration, automation).
- Sent as `Authorization: Bearer ts_…` or `X-Api-Key`.
- Bound to a **staff** user; hash stored in system settings (plaintext shown once on generate).
- Generate/revoke: Settings → staff API key (`super_admin` only). Treat like a password.

## CORS

- Policy `AllowFrontend`: localhost/127.0.0.1 (ports 3000/3011) and private LAN origins (10/8, 172.16–31, 192.168/16).
- Extra origins via `Cors:ExtraOrigins` in config.
- Credentials allowed; headers include `Authorization` and `X-Api-Key`.
- Do not open `ExtraOrigins` to `*` or untrusted public sites.

## Anonymous uploads path

- `GET /uploads/{**path}` is **AllowAnonymous** so `<img>` / download links work without sending JWT.
- Path traversal (`..`) is rejected; files must resolve under `wwwroot/uploads`.
- Filenames use non-enumerable GUIDs; still treat uploads as potentially sensitive (no directory listing).
- Prefer HTTPS and network ACL in production.

## Other notes

- Google Sheets sync endpoint is anonymous but gated by `Sync:ApiKey` (or equivalent header check).
- Knowledge Base published/search/suggest endpoints are intentionally public (`AllowAnonymous`).
- Default Development super-admin credentials must be changed before any shared/production deploy.
- Prefer `RabbitMQ:Enabled=false` (or in-memory) when Rabbit is not hardened/networked.
