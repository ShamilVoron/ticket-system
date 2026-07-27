# Field engineer guide (`/field`)

Mobile-oriented UI for role **field engineer** (`field_engineer` and related). Other roles are redirected away by middleware.

## Access

1. Sign in at `/auth/login` with a field-engineer account.
2. Open **`/field`** (or use the field layout navigation).
3. Non–field-engineer staff land on the main app (`/`).

## My tickets (`/field`)

- Lists tickets assigned to you (by user id / full name).
- Pull-to-refresh style **Update** button; live refresh via SignalR when tickets change.
- Tap a row to open ticket detail.

## Ticket detail (`/field/tickets/{id}`)

- See client/object, status, public comments, attachments.
- **Quick status** buttons (up to three common statuses such as «В работе», «Ожидание», «Решён»).
- Upload photos/files (attachments) from the device camera/gallery where the browser allows it.
- Open **field report** (акт выезда) for structured visit notes.

## Field report (`/field/report/{ticketId}`)

- Record visit date, action type (repair / install / replace / inspection / delivery / other).
- Equipment type, serial, status (in service / needs repair / written off / spare).
- Work done (required) and transfer notes.
- Save returns you to the ticket detail.

## Profile (`/field/profile`)

- Display name / role.
- Change password.
- Toggle light/dark theme.
- Log out.

## Tips

- Prefer the field UI on phones; main desk UI remains available for coordinators.
- Status and comments are visible to the client portal when not marked internal.
- If the list is empty, confirm the ticket assignees include your user (name or id) on the desk side.
