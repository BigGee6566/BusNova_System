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
   - If you're upgrading an existing database instead of recreating it, apply the incremental scripts in `App_Data/Migrations/` in order (`002_PasswordSetup.sql`, `003_RoleConsolidation.sql`, `004_BookingSchema.sql`, `005_TicketSeatIndexFilter.sql`, `006_SeatNumberNormalisation.sql`, `007_TicketNumberSequence.sql`, `008_UnicodeTextColumns.sql`, `009_PaymentGateway.sql`, `010_TicketValidation.sql`, `011_Notifications.sql`, `012_AnalyticsIndexes.sql`, `013_Cancellation.sql`, `014_MaintenanceManagement.sql`, `015_Tracking.sql`, `016_DiscriminatorConstraints.sql`, `017_RouteNameDataQuality.sql`, `018_MaintenanceAndStudentNumber.sql`).
   - **`006_SeatNumberNormalisation.sql`, `007_TicketNumberSequence.sql`, `008_UnicodeTextColumns.sql`, `009_PaymentGateway.sql`, `010_TicketValidation.sql`, `011_Notifications.sql`, `012_AnalyticsIndexes.sql`, `013_Cancellation.sql`, `014_MaintenanceManagement.sql`, `015_Tracking.sql` and `018_MaintenanceAndStudentNumber.sql` must also be run after a *fresh* create.** `007` creates the sequences ticket numbering now depends on, and `008` converts text columns to `nvarchar` so non-ASCII names are not silently corrupted; neither exists in the creation script. `009` adds the payment tables, `010` the ticket-validation log, `011` the notification queue, `012` analytics indexes, `013` cancellation columns, `014` maintenance schedules/parts/inspections, `015` tracking, and `018` the student number columns and indexes for active-trip duplicate prevention. None of them is in the creation script.
   - *(`006` specifically:)* Unlike the others it is not purely an upgrade step: the creation script still seeds two tickets with lettered seat labels (`12A`, `03B`), while the seat map issues plain integers. Until those rows are normalised the map treats seat `3` as free when `03B` is already sold. Correcting the seed itself is pending a separate decision.
   - *(`016` specifically:)* it adds CHECK constraints only — no foreign keys, and none are possible. See the note on `dbo.AuditLog` and `dbo.Notification` in [SYSTEM_OVERVIEW.md](SYSTEM_OVERVIEW.md#polymorphic-references-why-auditlog-and-notification-have-no-foreign-keys).
   - Run migrations with `sqlcmd -I` (or `SET QUOTED_IDENTIFIER ON`). `dbo.Ticket` carries a filtered index, and SQL Server rejects inserts from a session where that option is off — `sqlcmd` defaults it off.
3. Update the `BusNovaDb` connection string in [Web.config](Web.config) to point at your SQL Server instance.
4. Build and run (F5). The app opens on `Home.aspx`.

> **Encoding:** source files containing non-ASCII characters must be saved as **UTF-8 with BOM**. The `<globalization>` block in `Web.config` sets `fileEncoding="utf-8"`, but a file with no byte-order mark can still be misread by some tooling, which turns an em dash into `â€"`. In Visual Studio: *File → Save As → Save with Encoding → UTF-8 with signature*.

### Demo logins & System Credentials

See [CREDENTIALS.md](CREDENTIALS.md) for the complete list of system credentials across all roles (Manager, Driver, MaintenanceStaff, and Passengers).

To populate a rich 2-month dataset (1 month past, 1 month future) with 10 additional registered passengers, completed trips, fuel logs, maintenance records, and upcoming scheduled bookings, execute [App_Data/Seed2MonthDataset.sql](App_Data/Seed2MonthDataset.sql) in SQL Server. All newly seeded passengers use password `Passenger@2026`.

### Configuration

Key `appSettings` in Web.config:

> **Write decimal settings with a full stop, never a comma.** They are parsed with `InvariantCulture`, so `200.00` is two hundred. A comma is rejected and the documented default is used instead. This is deliberate: `200,00` used to parse as **20000** and charge a hundred times the fare (AUD-057).

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
| `BusNova:EtaRoadFactorLow` / `BusNova:EtaRoadFactorHigh` | How much longer a real road is than the straight line, used to widen the arrival window (defaults 1.15 and 1.45) |
| `BusNova:EtaAssumedSpeedKph` | Speed assumed when a bus reports none, or reports zero because it is stopped (default 65) |
| `BusNova:DynamicPricing` | Turns the WP-11 pricing rules on. **Off by default** — with it off every fare is the standard one |
| `BusNova:Pricing*` | The individual thresholds and percentages. See `Services/PricingService.cs`, where every rule is documented in order |

`Web.Release.config` swaps in placeholder connection-string credentials and enforces HTTPS-only cookies — replace the placeholders before deploying a release build.

## Verifying a change

```
.\Verify.ps1
```

Three stages — build, **markup**, tests — and a non-zero exit if any of them fails.

The middle stage is the one that is easy to skip and the reason the script exists. **Web Forms compiles `.aspx` markup on first request, not at build time**, so MSBuild reports a clean build for a page that will throw `HttpParseException` and return 500 the moment someone opens it. `aspnet_compiler` is the only way to find that before a user does, and it has caught six defects in this repo that MSBuild could not see.

So run it **after** every markup edit, not once per sitting. A `<%-- --%>` comment written inside an `<asp:Button>` tag shipped here exactly because the precompile had been run before that edit rather than after it.

The third stage carries what no compiler can. A misspelt `data-` attribute compiles perfectly and fails silently at runtime — `data-bn-confirm-body` instead of `data-bn-confirm` once left the driver's emergency button with no confirmation dialog at all, so a single press took a bus off the road without a prompt. `BusNova.Tests\MarkupGuardTests.cs` scans every page for that class of fault, because it knows what `Scripts/busnova-ui.js` actually reads.

`.\Verify.ps1 -SkipTests` runs the first two stages only, for a fast loop. It is not a verification.

## Running tests

Run the `BusNova.Tests` MSTest project from Visual Studio's Test Explorer, or:

```
vstest.console.exe BusNova.Tests\bin\Debug\BusNova.Tests.dll
```

258 tests. Coverage is unit tests plus markup guards: password hashing, password policy, setup tokens, roles, ticket pricing, seat validation (`TicketService.ValidateSeat`), the sign-in lockout decision (`LoginThrottle.Evaluate`), payments, refund policy, notifications and analytics, maintenance scheduling, table export, tracking, dynamic pricing, the local-URL open-redirect guard (`LocalUrl`), and the markup guards described above. Services that reach the database are still not covered — the service tests target logic deliberately written to be callable without one.

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
├── BusNova.Tests/      MSTest unit tests and markup guards
├── Verify.ps1          build + markup precompile + tests
└── *.aspx / *.aspx.cs  Web Forms pages
```
