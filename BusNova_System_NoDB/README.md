# BusNova System

BusNova is an ASP.NET Web Forms application for managing a small bus operator's day-to-day work: fleet and route setup, driver/bus scheduling, fuel and maintenance logging, and passenger ticket sales/booking. It was built as a university systems-development coursework project (previously named "CyberNova").

## Tech stack

- **Framework:** ASP.NET Web Forms, .NET Framework 4.7.2 (C#)
- **Data access:** hand-rolled ADO.NET (`System.Data.SqlClient`) against SQL Server — no Entity Framework
- **Database:** SQL Server (developed against a local SQL Express instance)
- **Front end:** Bootstrap 5.3.7, Bootstrap Icons, Font Awesome 6.5.1 (all via CDN, no npm/build step)
- **Auth:** custom session-based authentication (not ASP.NET Forms/Windows auth)
- **Tests:** MSTest v2 (`BusNova.Tests`)

See [SYSTEM_OVERVIEW.md](SYSTEM_OVERVIEW.md) for architecture, pages, services, roles, and database schema.

## Getting started

### Prerequisites

- Visual Studio 2019/2022 with ASP.NET / web development workload
- SQL Server or SQL Server Express
- IIS Express (bundled with Visual Studio)

### Setup

1. Open `BusNova System.sln` in Visual Studio.
2. Create the database by running [App_Data/BusNova_Database_Script.sql](App_Data/BusNova_Database_Script.sql) against your SQL Server instance. This creates the `BusNovaDB` database, its tables, and seed/demo data.
   - If you're upgrading an existing database instead of recreating it, apply the incremental scripts in `App_Data/Migrations/` in order (`002_PasswordSetup.sql`, `003_RoleConsolidation.sql`, `004_BookingSchema.sql`, `005_TicketSeatIndexFilter.sql`, `006_SeatNumberNormalisation.sql`, `007_TicketNumberSequence.sql`, `008_UnicodeTextColumns.sql`, `009_PaymentGateway.sql`, `010_TicketValidation.sql`, `011_Notifications.sql`, `012_AnalyticsIndexes.sql`, `013_Cancellation.sql`, `014_MaintenanceManagement.sql`).
   - **`006_SeatNumberNormalisation.sql`, `007_TicketNumberSequence.sql`, `008_UnicodeTextColumns.sql`, `009_PaymentGateway.sql`, `010_TicketValidation.sql`, `011_Notifications.sql`, `012_AnalyticsIndexes.sql`, `013_Cancellation.sql` and `014_MaintenanceManagement.sql` must also be run after a *fresh* create.** `007` creates the sequences ticket numbering now depends on, and `008` converts text columns to `nvarchar` so non-ASCII names are not silently corrupted; neither exists in the creation script. `009` adds the payment tables, which the booking flow writes to on every purchase - without it, confirming a payment fails at the first insert. `010` adds the ticket-validation log the door screen writes to, `011` the notification queue, `012` the missing foreign-key and analytics indexes, `013` the cancellation and refund columns on `dbo.Ticket`, and `014` the maintenance schedules, parts, labour and trip inspections. None of them is in the creation script.
   - *(`006` specifically:)* Unlike the others it is not purely an upgrade step: the creation script still seeds two tickets with lettered seat labels (`12A`, `03B`), while the seat map issues plain integers. Until those rows are normalised the map treats seat `3` as free when `03B` is already sold. Correcting the seed itself is pending a separate decision.
   - Run migrations with `sqlcmd -I` (or `SET QUOTED_IDENTIFIER ON`). `dbo.Ticket` carries a filtered index, and SQL Server rejects inserts from a session where that option is off — `sqlcmd` defaults it off.
3. Update the `BusNovaDb` connection string in [Web.config](Web.config) to point at your SQL Server instance.
4. Build and run (F5). The app opens on `Home.aspx`.

> **Encoding:** source files containing non-ASCII characters must be saved as **UTF-8 with BOM**. The `<globalization>` block in `Web.config` sets `fileEncoding="utf-8"`, but a file with no byte-order mark can still be misread by some tooling, which turns an em dash into `â€"`. In Visual Studio: *File → Save As → Save with Encoding → UTF-8 with signature*.

### Demo logins

The database script seeds demo accounts for each role (Manager, Driver, MaintenanceStaff) plus a demo Passenger. See the `INSERT` statements for `dbo.Employee` / `dbo.Passenger` in the database script for the seeded usernames/emails.

### Configuration

Key `appSettings` in Web.config:

| Setting | Purpose |
|---|---|
| `BusNova:LogDirectory` | Where unhandled exceptions are logged (`~/App_Data/Logs`) |
| `BusNova:TicketPrice` | Flat ticket fare used by `TicketService` |
| `BusNova:ServiceIntervalMonths` | Interval used to compute a bus's next service due date |
| `BusNova:LoginMaxAttempts` | Consecutive failed sign-ins before an address is refused (default 5) |
| `BusNova:LoginLockoutMinutes` | How long that refusal lasts, and how far back failures are counted (default 15) |
| `BusNova:PaymentSandbox` | Marks payment rows as sandbox rows (default `true`). There is no real gateway in this build. |
| `BusNova:PaymentSandboxForceOutcome` | Forces every payment to `decline` or `fail`, for demonstrating those paths. Empty or absent means normal behaviour. Server-side only - it replaced a checkbox that passengers could see and tick (AUD-045). |
| `BusNova:NotificationMaxAttempts` | Delivery attempts before a notification is dead-lettered (default 5) |
| `BusNova:RefundFullHours` | Hours before departure up to which a cancellation is refunded in full (default 24) |
| `BusNova:RefundPartialHours` | Hours before departure up to which a partial refund applies (default 2) |
| `BusNova:RefundPartialPercent` | The percentage refunded in that partial tier (default 50) |
| `BusNova:TripStartGraceMinutes` | How early a driver may start a trip before its scheduled departure (default 60) |
| `BusNova:DepotAlertEmail` | Where driver delay and emergency alerts are queued (default `depot@busnova.co.za`) |

`Web.Release.config` swaps in placeholder connection-string credentials and enforces HTTPS-only cookies — replace the placeholders before deploying a release build.

## Running tests

Run the `BusNova.Tests` MSTest project from Visual Studio's Test Explorer, or:

```
vstest.console.exe BusNova.Tests\bin\Debug\BusNova.Tests.dll
```

Current coverage is unit tests only: password hashing, password policy, setup tokens, roles, ticket pricing, seat validation (`TicketService.ValidateSeat`) and the sign-in lockout decision (`LoginThrottle.Evaluate`). 57 tests. Services that reach the database are still not covered — the two service tests above target logic deliberately written to be callable without one.

## Project layout

```
BusNova System/
├── Services/        Business logic (static service classes over ADO.NET)
├── Security/         Auth, password hashing, CSRF, session helpers
├── Models/           Domain entities and constants (Entities.cs)
├── Helpers/          Shared UI rendering helpers (seat map, grid labels, colors)
├── DataAccess/        Db.cs — SqlConnection/SqlCommand wrapper
├── Styles/, Scripts/   Site CSS and JS
├── App_Data/          Database script, migrations, logs
├── BusNova.Tests/      MSTest unit tests
└── *.aspx / *.aspx.cs  Web Forms pages
```
