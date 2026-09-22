# BusNova System — System Overview

BusNova is an ASP.NET Web Forms application (.NET Framework 4.7.2) for a small bus operator, covering fleet/route setup, driver scheduling, fuel and maintenance logging, and passenger ticketing/booking. This document describes the architecture, roles, pages, services, and database schema as they currently stand in the working tree (which includes substantial work not yet committed to git — see [Development status](#development-status)).

## Architecture

- **UI:** ASP.NET Web Forms (`.aspx` + `.aspx.cs` code-behind), one shared `Site.Master` layout, Bootstrap 5.3.7 / Bootstrap Icons / Font Awesome via CDN. No SPA framework or build step.
- **Business logic:** static service classes in `Services/`, called directly from code-behind. No DI container.
- **Data access:** hand-rolled ADO.NET (`System.Data.SqlClient`) in `DataAccess/Db.cs`, executing raw parameterized SQL. No ORM.
- **Database:** SQL Server (`BusNovaDB`).
- **Auth:** custom session-based auth (`Security/SessionHelper.cs`), not ASP.NET Forms/Windows auth (`authentication mode="None"` in Web.config).

## User roles

Two DB-level user categories — **Employee** (staff) and **Passenger** — with four functional roles:

| Role | Table | Capabilities |
|---|---|---|
| **Manager** | Employee | Register buses, create routes, assign bus+driver to routes, manage staff/passenger accounts, issue/validate tickets at a counter. Consolidates what were formerly separate Administrator and TicketConductor roles. |
| **Driver** | Employee | View own schedule, start/complete trips, log fuel, report maintenance faults. Requires a license number/expiry. |
| **MaintenanceStaff** | Employee | View/handle maintenance work orders, log completed service. Requires a specialisation. |
| **Passenger** | Passenger | Self-register or get registered by a conductor as a walk-up (one-time password-setup link), book trips, view/cancel own tickets. |

Every protected page enforces its own role check server-side via `SessionHelper.RequireLogin`/`RequireRole` as the first statement in `Page_Load` — navigation menu visibility in `Site.Master` is a convenience only, not a security boundary. Staff accounts can only be created by a Manager (`ManageUsers.aspx`); the public `Register.aspx` page is passenger-only, preventing privilege escalation.

## Pages

### Public / authentication
- **Home.aspx** — public landing page.
- **Login.aspx** — sign-in, redirects via `returnUrl`.
- **Logout.aspx** — clears session.
- **Register.aspx** — public passenger self-registration.
- **RequestPasswordLink.aspx** — request/re-issue a one-time password-setup link for a walk-up passenger.
- **SetPassword.aspx** — consume that link and set a real password (no login required).
- **Error.aspx** — friendly error page for 404/403/500.

### Shared
- **Dashboard.aspx** — one page for all roles, content branches by role.

### Manager
- **RegisterBus.aspx** — register a bus (unique registration number, capacity, fuel type, year).
- **CreateRoute.aspx** — create a route (unique code/name, origin/destination/distance/duration).
- **AssignRoute.aspx** — assign a bus + driver to a route/date/time, with conflict and eligibility checks.
- **ManageUsers.aspx** — create staff accounts, activate/deactivate staff and passengers.
- **IssueTicket.aspx** — counter ticket issuance: register/find a passenger, pick a trip and seat, purchase a ticket, validate an existing ticket by number.

### Driver
- **ViewAssignedRoutes.aspx** — own schedule; start/complete trip actions.
- **LogFuel.aspx** — log a fuel purchase, optionally tied to a trip.
- **ReportIssue.aspx** — report a bus fault, creating a maintenance log entry.

### MaintenanceStaff
- **MaintenanceWork.aspx** — view open/all maintenance work, start work, log completed service (parts, cost, next service date).

### Passenger
- **BookTrip.aspx** — search trips, book 1–5 seats via a seat map; saves a draft to session before payment.
- **PaymentSimulation.aspx** — simulated payment (Card/MobileWallet/EFT); nothing is written to the database until this succeeds.
- **TicketConfirmation.aspx** — confirmed booking / e-tickets per traveller.
- **MyTickets.aspx** — own ticket/booking history; cancel a still-valid, not-yet-departed ticket.

## Services (`Services/`)

Static classes calling `DataAccess/Db.cs` directly.

- **AuthService** — sign-in against Employee/Passenger, passenger self-registration, and the walk-up-passenger password-setup flow (issue/re-issue/consume one-time tokens). Uses a generic "incorrect email or password" message to avoid user enumeration.
- **AssignmentService** — route/bus/driver assignment and the driver's schedule. Validates route/bus/driver existence, active bus, active non-expired driver license, no past dates, capacity limits, and bus/driver time-window conflicts; also handles start/complete trip and dashboard trip counters.
- **TicketService** — single-seat ticket purchase and multi-traveller booking (1–5 seats), ticket/booking lookups, cancellation (own, valid, not-yet-departed tickets only). Generates `BN-######` ticket numbers and `BNB-######` booking references inside transactions guarded against race conditions.
- **AdminService** — staff/passenger account administration: create employees (role-specific required fields, uniqueness checks, minimum password length), activate/deactivate accounts.
- **FleetService** — buses and routes reference data: registration, uniqueness checks, status updates, active route/driver lookups.
- **OperationsService** — fuel logging, fault reporting, and maintenance work (open/all lists, start work, log completed service).
- **AuditService** — writes to `dbo.AuditLog` for actions and login attempts; failures are swallowed so audit logging never breaks the primary operation.

## Security (`Security/`)

- **SessionHelper** — session-based sign-in/out, role checks, `RequireLogin`/`RequireRole` page guards, shared dashboard routing.
- **PasswordHasher** — PBKDF2-SHA256, 100,000 iterations, random salt, constant-time verification.
- **PasswordPolicy** — passenger self-registration password rules (length, character classes) and a strength scorer.
- **PasswordSetupToken** — separate SHA-256 token scheme (256-bit random, 48-hour validity, constant-time verify) for one-time walk-up-passenger password-setup links.
- **AntiForgery** — synchronizer-token CSRF protection: one per-session token rendered into a hidden field on the shared form, checked on state-changing postbacks.
- **BookingDraftSession** — carries an in-progress multi-traveller booking (assignment + traveller list) in session between `BookTrip.aspx` and `PaymentSimulation.aspx`.

## Domain model (`Models/Entities.cs`)

Constants: `Roles`, `TripStatus` (Scheduled/InProgress/Completed/Cancelled), `BusStatus` (Active/UnderMaintenance/Retired), `MaintenanceStatus` (Reported/InProgress/Completed/Cancelled), `PaymentMethods` (Card/MobileWallet/EFT).

Entities: `Employee`, `Passenger`, `Bus`, `Route`, `RouteAssignment`, `FuelLog`, `MaintenanceLog`, `Ticket`, `Booking`, `BookingTraveller` (transient booking-form DTO, not persisted directly).

## Database schema (`App_Data/BusNova_Database_Script.sql`)

SQL Server database `BusNovaDB`:

| Table | Purpose |
|---|---|
| `dbo.Employee` | Staff accounts; role-specific fields enforced by CHECK constraints (license for Driver, specialisation for MaintenanceStaff). |
| `dbo.Passenger` | Passenger accounts, including walk-up password-setup-token fields. |
| `dbo.Bus` | Fleet vehicles, capacity, status, next service due date. |
| `dbo.Route` | Route reference data (business code, origin/destination/distance/duration). |
| `dbo.RouteAssignment` | A bus+driver assigned to a route on a date/time. `UQ_Assignment_BusSlot` and `UQ_Assignment_DriverSlot` prevent an **identical** slot only - same bus/driver, same date, same departure time. They do **not** prevent an **overlapping** one, and no unique index can: an overlap is a range comparison, and a unique constraint has no notion of a range. The application-level check in `AssignmentService.HasBusConflict` / `HasDriverConflict` is therefore the only defence against an overlapping double-booking, not a convenience in front of a database backstop. (AUD-013) |
| `dbo.FuelLog` | Fuel purchases per bus/driver/trip. |
| `dbo.MaintenanceLog` | One row spans a fault's life, from driver report through maintenance close-out. |
| `dbo.Booking` | A multi-traveller booking (reference, total amount, payment status/method). |
| `dbo.Ticket` | Individual tickets, optionally linked to a booking; filtered unique indexes prevent double-booking a seat or a duplicate traveller name within a booking. |
| `dbo.AuditLog` | Action/login audit trail. |

Incremental migration scripts in `App_Data/Migrations/` (`002_PasswordSetup.sql`, `003_RoleConsolidation.sql`, `004_BookingSchema.sql`) apply the same schema evolution to an existing database without a full recreate.

## Configuration (`Web.config`)

- Connection string `BusNovaDb` (SQL Server, integrated security in dev).
- App settings: `BusNova:LogDirectory`, `BusNova:TicketPrice` (flat fare, default 185.00), `BusNova:ServiceIntervalMonths`.
- Session-based auth (`authentication mode="None"`), 30-minute in-process session state, HttpOnly cookies.
- `customErrors` routes 404/403/500 to `Error.aspx`; unhandled exceptions are additionally logged to `App_Data/Logs`.
- Security headers added at the `system.webServer` level (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`); `X-Powered-By` removed.
- `Web.Release.config` forces HTTPS-only cookies and replaces the connection string with placeholder credentials that must be filled in before a release build is deployed.

No email-sending or PDF-generation capability exists in the codebase — the password-setup "link" is a token embedded in a URL that a conductor hands to the passenger directly, not sent by email.

## Testing (`BusNova.Tests/`)

MSTest v2 unit tests covering `Security` and `Models` logic: password hashing, password policy/strength scoring, password setup tokens, role constants, and ticket pricing. Services that depend on the database are not currently covered by automated tests.

## Development status

This project originated as a university systems-development coursework deliverable (formerly named "CyberNova"); code comments reference formal use-case (UC) and requirement (R) numbers from that spec. Git history reflects a staged build-out (UI-only baseline → SQL Server connection → fuel/maintenance/ticketing services), and the current working tree contains substantial functionality — most passenger-facing pages, booking/multi-traveller support, maintenance work, user administration, and the `Security`/`Helpers`/`BusNova.Tests` folders — not yet committed to git.
