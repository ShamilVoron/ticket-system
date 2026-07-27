# ADR 001: Multi-tenant preparation

## Status

Accepted (schema prep only — Phase 9)

## Context

Ticet is currently a single-deployment helpdesk. A future SaaS offering may host multiple organizations in one database. Full multi-tenancy (row-level filtering, org switching, billing) is out of scope for Phase Beta.

## Decision

### Phase Beta (now)

- **Single-tenant, self-hosted.** One installation serves one organization.
- No query filtering by tenant. No billing, plans, or org admin UI.

### Future multi-tenant shape

When SaaS becomes real:

1. **`Organization` table** — tenants (name, slug, timestamps).
2. **`OrganizationId` on tenant-scoped entities** — Ticket, Company, Employee, UserAccount, ChatConversation (and likely more later).
3. **`ITenantProvider`** — resolves the current organization for the request (claim, subdomain, or host mapping).
4. **No billing yet** in this ADR; billing is a separate later decision.

### Nullable `OrganizationId` with default org `Id = 1`

- Columns are **`int? OrganizationId`** so existing rows and Phase Beta writes remain valid without mandatory backfill.
- A seed row **`Organization { Id = 1, Name = "Default" }`** is the implicit tenant for single-tenant mode.
- `SingleTenantProvider` always returns `CurrentOrganizationId => 1`.
- Later: make `OrganizationId` required and enforce filters via `ITenantProvider`; until then, do **not** filter all queries.

## Consequences

- Schema and DI stub are ready without changing runtime behavior.
- Migration adds `Organizations` plus nullable `OrganizationId` columns/indexes.
- Call sites must not assume multi-tenant isolation until filtering is implemented.
