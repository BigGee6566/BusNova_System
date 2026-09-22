# BusNova — Stage 1 Audit Findings

**Date:** 2026-09-21
**Scope:** Full checklist A–J.
**Method:** Read of the working tree, plus a clean `msbuild` of `BusNova System.sln`, a full run of the `BusNova.Tests` MSTest suite, an offline cryptographic verification of the seeded password hashes, and a **read-only** inspection of the live `BusNovaDB` reachable through the `BusNovaDb` connection string in `Web.config`.
**Not done:** the application was not deployed or browsed, no database was created or modified, and no row was written. Anything that needed a running site or a fresh database is marked **Suspected** with the exact action required to settle it.

Commit state at audit time: branch `master`, HEAD `9d7228f`, with substantial uncommitted work in the tree.

---

## 1. Summary

| Severity | Confirmed | Suspected | Resolved | Not reproducible | Total |
|---|---|---|---|---|---|
| Critical | 2 | 0 | 1 | 0 | 3 |
| High | 10 | 0 | 1 | 1 | 12 |
| Medium | 16 | 1 | 2 | 0 | 19 |
| Low | 18 | 0 | 0 | 0 | 18 |
| **Total** | **46** | **1** | **3** | **1** | **51** |

> **Count correction (2026-09-22):** the summary table published with Stage 1 read 38 findings and did not match the 47 rows actually listed below it — it was written by hand rather than tallied from the table, and undercounted the rolled-up Low findings. The figures above are now computed directly from the findings table and are authoritative. No finding was added or removed by this correction; only the arithmetic changed. The four findings added after Stage 1 (AUD-048 … AUD-051) account for the remainder of the difference.

> **Addendum, 2026-09-21 (post-review):** AUD-048, AUD-049 and AUD-050 were discovered after the original sweep and are recorded here before any fix work, per the no-silent-scope-creep rule. AUD-048 and AUD-049 were found while scoping WP-0b; AUD-050 was found while editing `<head>` during FIX-014. They are the only findings in this document not produced by the original Stage 1 sweep. **AUD-048 is now Resolved.**

The build is clean, the test suite is green, the CSRF and page-guard coverage is genuinely complete, the password hashing is exactly what the documentation claims, and every SQL statement in the codebase is parameterised. Those are real strengths and the audit confirmed each one by evidence rather than by reading the comments.

The problems are concentrated in four places:

1. **No reporting exists at all.** Not one of the four reports the Project 6 brief requires is implemented, and neither is any report named in the Sprint system matrices. This is the single largest gap against the brief.
2. **The passenger booking path has never successfully completed.** The live database holds zero `Booking` rows, zero `BookingCreated` audit entries, and every `Ticket` row has a `NULL BookingID`. Two confirmed defects in that path explain why it would fail under tampering, and it is unexercised even under normal use.
3. **The deployed database does not match its own creation script.** The seat-uniqueness index is unfiltered in the live database, which permanently blocks resale of any cancelled seat, and no migration script exists that would ever fix it.
4. **Passenger-facing ticket numbers are derived from `MAX(TicketID)+1`, not from the identity value.** The live data proves the two have already diverged.

---

## 2. Findings table

Severity: **Critical** (security, data loss, workflow cannot complete) · **High** (feature present but broken or wrong) · **Medium** (missing promised feature, inconsistency) · **Low** (cosmetic, docs drift).

| ID | Area | Severity | Description | Evidence (file:line) | Status |
|---|---|---|---|---|---|
| AUD-001 | F Reporting | Critical | None of Project 6's four required reports exists as a page. There is no report page of any kind in the application; `ReportIssue.aspx` is a driver fault-report form, not a report. | Repo root — no `*Report*.aspx` other than `ReportIssue.aspx`; `Site.Master:93-205` had no report nav item for any role | **Resolved** (FIX-001, 2026-09-22) — all four Project 6 reports delivered as `ReportRouteCompletion.aspx`, `ReportFuelEfficiency.aspx`, `ReportDriverPerformance.aspx`, `ReportMaintenanceDue.aspx`, backed by `Services/ReportingService.cs`. Every KPI verified against an independently written SQL query. The Sprint matrix reports remain outstanding and are scoped to WP-9. |
| AUD-002 | E Data integrity | Critical | In the live `BusNovaDB`, `UQ_Ticket_AssignmentSeat` is an **unfiltered** unique index on `(AssignmentID, SeatNumber)`. Once a ticket is cancelled its seat can never be sold again on that trip, because the cancelled row still occupies the key. `TicketService.GetTakenSeats` treats the seat as free, so the seat map offers it, and the insert then dies on a unique violation reported to the user as "taken a moment ago by another passenger". | `sys.indexes`: `UQ_Ticket_AssignmentSeat`, `has_filter = 0`, vs `App_Data/BusNova_Database_Script.sql:482-484` which declares `WHERE TicketStatus <> 'Cancelled'`; misleading message at `Services/TicketService.cs:351-353`. **Correction (2026-09-22):** it is not merely an unfiltered index - it is a UNIQUE **constraint** (present in `sys.key_constraints` as `UNIQUE_CONSTRAINT`), i.e. the deployed database predates script revision 12 entirely. Same practical effect, but it must be removed with `ALTER TABLE ... DROP CONSTRAINT`, not `DROP INDEX`; migration 005 handles both forms. One dead seat existed: `BN-001004`, seat `1` on assignment 7 | **Resolved** (migration 005, 2026-09-22) |
| AUD-003 | A Build / E | High | No migration script rebuilds `UQ_Ticket_AssignmentSeat` as a filtered index. `004_BookingSchema.sql` creates `UQ_Ticket_BookingTraveller` but never touches the seat index, and there is no `005`. Any database not recreated from scratch keeps the AUD-002 defect forever — including the one this application is configured against. | `App_Data/Migrations/004_BookingSchema.sql:70-81` (creates only the traveller index) | **Resolved** (2026-09-22) — `005_TicketSeatIndexFilter.sql` written and applied; a full schema diff against a database built fresh from the script now shows zero differences |
| AUD-004 | E Data integrity | High | `BN-######` ticket numbers are generated as `SELECT ISNULL(MAX(TicketID),0)+1`, which is **not** the identity value the row actually receives. After a SQL Server identity-cache gap the two diverge permanently, producing a passenger-facing number unrelated to the record and a visibly broken sequence. | `Services/TicketService.cs:311-317` and `:452-458`; live proof — `TicketID 1003` carries `TicketNumber BN-000005` | **Resolved** (FIX-004 / migration 007, 2026-09-22) — replaced with SQL Server `SEQUENCE` objects (`NO CACHE`) starting above the highest number already issued. Proven by issuing a ticket through the counter screen after deliberately burning one sequence value: the old logic would have produced `BN-001007`, the sequence produced `BN-001008`, on a row whose identity had jumped to 1011. Existing numbers deliberately not rewritten. |
| AUD-005 | D Workflows | High | `BookTrip.btnContinueToPayment_Click` loops over the seat list posted in the `hfSelectedSeats` hidden field and indexes a fixed five-element `TextBox[]` with the loop counter. A tampered field containing six or more seats throws `IndexOutOfRangeException` and drops the passenger on the error page instead of showing a validation message. The 1–5 cap is enforced only later, in the service, which is never reached. | `BookTrip.aspx.cs:158-174` (unbounded `selectedSeats`), `BookTrip.aspx.cs:27-30` (five boxes); cap at `Services/TicketService.cs:378-380` | **Resolved** (FIX-005, 2026-09-22) — verified at runtime: posting six seats now returns "at most 5 seats in one booking" and the error log is byte-identical, i.e. no unhandled exception. A duplicate-seat guard was added alongside it. |
| AUD-006 | D Workflows | High | `TicketService.CreateBooking` never validates seat-number length or that the seat exists on the bus. `Ticket.SeatNumber` is `VARCHAR(5) NOT NULL`, so a tampered seat longer than five characters raises a string-truncation `SqlException`, which is not a unique violation and is therefore rethrown unhandled. A seat outside the bus layout (e.g. `99Z` on a 45-seater) is accepted and sold. `PurchaseTicket` does check length; `CreateBooking` does not. | `Services/TicketService.cs:382-396` (no length/range check) vs `Services/TicketService.cs:279-280`; column type confirmed in live `sys.columns` — `SeatNumber varchar(5) NOT NULL` | **Resolved** (FIX-006, 2026-09-22) — `TicketService.ValidateSeat` now guards both `CreateBooking` and `PurchaseTicket` against over-length and out-of-range seats; 13 new MSTest cases cover it. |
| AUD-007 | D Workflows | High | Nothing in the codebase ever sets `TicketStatus = 'Used'`. `IssueTicket.btnValidate_Click` looks a ticket up and prints its status but changes nothing, so the same ticket validates as `Valid` an unlimited number of times and no boarding is ever recorded. The only `Used` row in the database came from the seed script. | `IssueTicket.aspx.cs:301-320` (read-only); grep for `'Used'` across `Services/` and `*.aspx.cs` returned only two read comparisons | **Resolved** (FIX-007, 2026-09-22) — `TicketService.MarkTicketUsed` added and wired to an explicit "Mark as boarded" action. Verified end to end: Valid → boarded → re-lookup shows Used and offers no boarding; a cancelled ticket is never offered boarding; one `TicketBoarded` audit row written. Concurrency proven separately: two simultaneous UPDATEs affect 1 row and 0 rows. |
| AUD-008 | E Data integrity | High | Seat identifiers exist in two incompatible formats. `SeatMapRenderer` numbers seats `1..capacity` as bare integers, but the shipped seed data uses `12A` / `03B`. A seeded ticket on `03B` does not block the seat the map calls `3`, so on a freshly seeded database the seat map will sell a seat that is already occupied. Live data shows both formats coexisting. | `Helpers/SeatMapRenderer.cs:14-18, 60-70`; seed rows at `App_Data/BusNova_Database_Script.sql:640-644` | **Resolved for existing databases** (FIX-008 / migration 006, 2026-09-22) — 4 rows normalised (`12A`→12, `03B`→3), collision-free, verified: `GetTakenSeats(4)` now returns 3, 12, 23 and a second sale of seat 3 is rejected. **Open for fresh databases:** the creation script still seeds the lettered labels and project rules forbid editing it, so migration 006 must be run after a fresh create too. Correcting the seed needs a separate decision. |
| AUD-009 | B Auth | High | The ASP.NET session identifier is never regenerated on sign-in. `SessionHelper.SignIn` calls `Session.Clear()`, which empties the contents but keeps the same session id, leaving the application open to session fixation: an attacker who plants a session id in a victim's browser still holds a valid id after the victim authenticates. | `Security/SessionHelper.cs:32-42` | Confirmed |
| AUD-010 | B Auth | High | There is no rate limiting, throttling or account lockout on sign-in. Failed attempts are written to `dbo.AuditLog` but nothing reads them, so an unlimited online password-guessing attack against any known address is possible. | `Services/AuthService.cs:38-55` (no attempt counting); `Services/AuditService.cs:251-274` (write-only) | Confirmed |
| AUD-011 | B Auth | High | Three different password rules govern three account-creation paths, and the weakest rule protects the most privileged accounts. Staff accounts — including Manager — require only eight characters of any kind; walk-up passengers setting a password require only `.{8,}`; only public passenger self-registration enforces the full `PasswordPolicy` (length plus four character classes). | Staff: `Services/AdminService.cs:93-94`; walk-up: `SetPassword.aspx:36` (`ValidationExpression=".{8,}"`) with no server-side policy call in `SetPassword.aspx.cs:52-73`; passengers: `Register.aspx.cs:59-66` | Confirmed |
| AUD-012 | C Security | High | Security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`) and the `X-Powered-By` removal are declared in `Web.config` but were not observed on a live response. Declaration is not emission — a `customHeaders` block can be overridden or ignored depending on the IIS configuration the site is actually served under. | `Web.config:74-83` | **Not reproducible** (2026-09-22) — site run under IIS Express; `GET /Home.aspx` returned all three headers and no `X-Powered-By`. Not a defect; FIX-013 cancelled. |
| AUD-013 | E Data integrity | Medium | `UQ_Assignment_BusSlot` and `UQ_Assignment_DriverSlot` are unique on `(BusID/EmployeeID, AssignmentDate, DepartureTime)`. They prevent only an *identical* slot, not an *overlapping* one, which a unique index cannot express. Two overlapping trips at different departure times pass the index, so the "database backstop" for the race the code explicitly documents does not exist for overlaps. | Live `sys.indexes` key columns; `App_Data/BusNova_Database_Script.sql:273-281`; the claim at `Services/AssignmentService.cs:36-41` | Confirmed |
| AUD-014 | D Workflows | Medium | `HasBusConflict` / `HasDriverConflict` both filter on `ra.AssignmentDate = @date`. A trip that starts late on the previous day and runs past midnight into the proposed window is invisible to both checks, so a bus or driver can be double-booked across a midnight boundary. | `Services/AssignmentService.cs:163` and `:190` | Confirmed |
| AUD-015 | D Workflows | Medium | Cancellation compares dates only, so a passenger can cancel a ticket for a trip that departed earlier the same day and receive a refund status for a journey that has already run. | `Services/TicketService.cs:522` and `MyTickets.aspx.cs:207-212` (both compare `.Date` only, ignoring `DepartureTime`) | Confirmed |
| AUD-016 | D Workflows | Medium | `AssignmentService.StartTrip` applies no date or time gating. A driver can start a trip scheduled weeks in the future, and nothing checks that the assigned bus is still `Active` at the moment the trip starts. | `Services/AssignmentService.cs:341-361` | Confirmed |
| AUD-017 | E Data integrity | Medium | Multi-statement writes are not wrapped in transactions. `LogService` issues up to four separate `Db.Execute` calls (maintenance row, bus next-service date, bus odometer, bus status); `LogFuel` issues two; `ReportIssue` issues two. A failure between them leaves the maintenance record closed while the bus row still says it is under maintenance, or a fuel row recorded against a stale odometer. | `Services/OperationsService.cs:343-387`, `:142-155`, `:273-285` | Confirmed |
| AUD-018 | E Data integrity | Medium | Ticket and booking number generation takes `WITH (TABLOCKX)` on `dbo.Ticket` and `dbo.Booking` inside the transaction. This is correct but serialises every ticket sale in the system behind one exclusive table lock, and in `CreateBooking` the lock is retaken once per traveller inside the loop. | `Services/TicketService.cs:311-313`, `:426-428`, `:452-454` | **Resolved** (FIX-004, 2026-09-22) — the `TABLOCKX` existed only to make `MAX(id)+1` safe. A sequence needs no lock, so it was removed rather than reduced; ticket sales no longer serialise and the per-traveller re-lock inside the booking loop is gone. Folded out of WP-1. |
| AUD-019 | E Data integrity | Medium | Seven foreign keys have no supporting index on the referencing column: `FK_Bus_CreatedBy`, `FK_Route_CreatedBy`, `FK_Assignment_CreatedBy`, `FK_FuelLog_Employee`, `FK_FuelLog_Assignment`, `FK_Maintenance_Employee`, `FK_Maintenance_ServicedBy`. | Live `sys.foreign_keys` joined against `sys.index_columns`; index list at `App_Data/BusNova_Database_Script.sql:459-473` | Confirmed |
| AUD-020 | D Workflows | Medium | The multi-traveller booking path has never completed successfully against the live database: `dbo.Booking` holds zero rows, `dbo.AuditLog` contains no `BookingCreated` action, and every `Ticket` row has `BookingID` `NULL`. All six `TicketPurchased` entries came from the counter flow (`PurchaseTicket`), not from `CreateBooking`. The feature is, at minimum, entirely unexercised. | Live `SELECT COUNT(*) FROM dbo.Booking` = 0; `AuditLog` action list contains no `BookingCreated`; all `Ticket.BookingID` `NULL` | Confirmed |
| AUD-021 | B Auth | Medium | `AuthService.Authenticate` runs the 100,000-iteration PBKDF2 verification only when a matching row exists. An unknown address returns in microseconds while a known address takes tens of milliseconds, so the response time reveals which addresses are registered despite the deliberately generic failure message. | `Services/AuthService.cs:74` and `:120` (early `return null` before any hashing) | Confirmed |
| AUD-022 | C Security | Medium | The one-time password-setup token travels in the URL query string, where it is written to IIS request logs, browser history, and any `Referer` header sent to the CDN-hosted assets the page loads. The token is correctly hashed at rest and correctly single-use; the transport is the weak point. | `IssueTicket.aspx.cs:229-231`; consumed at `SetPassword.aspx.cs:36-47` | Confirmed |
| AUD-023 | D Workflows | Medium | There is no refund policy. Cancellation flips `PaymentStatus` to `Refunded` unconditionally, with no cut-off window, no partial refund, no reason code, and no record of who cancelled or when. | `Services/TicketService.cs:525-531` | Confirmed |
| AUD-024 | I UI/UX | Medium | 64 inline `style="…"` attributes across 17 pages, plus one HTML fragment with an inline style built in C# and written to the page. | `grep -c 'style="'` per page — `IssueTicket.aspx` 17, `BookTrip.aspx` 6, `MaintenanceWork.aspx` 6, `ManageUsers.aspx` 5, `TicketConfirmation.aspx` 5, `LogFuel.aspx` 4, `PaymentSimulation.aspx` 4, `ViewAssignedRoutes.aspx` 4, others 1–3; C#-built inline style at `Dashboard.aspx.cs:209` | Confirmed |
| AUD-025 | I UI/UX | Medium | Three destructive actions are gated by the browser's native `confirm()` dialog rather than an in-page confirmation, so they cannot be styled, cannot be made accessible, and are suppressible by the browser. | `ManageUsers.aspx:158`, `ManageUsers.aspx:201`, `MyTickets.aspx:74` | Confirmed |
| AUD-026 | I UI/UX | Medium | `Styles/SeatMap.css` and `Styles/style.css` contain no media queries at all. The seat map is the core of the booking journey and has no narrow-screen handling; the smallest breakpoint anywhere in the stylesheets is 480px. | `grep -c "@media"` → `SeatMap.css` 0, `style.css` 0; smallest breakpoint `Styles/Ops.css:252` (`max-width: 480px`) | Confirmed |
| AUD-027 | I UI/UX | Medium | Whether any page actually breaks at 360px was not verified — no browser was run. Given AUD-026 the seat map is the prime suspect. | `Styles/SeatMap.css` (no breakpoints) | **Resolved** (2026-09-22) — measured under CDP device emulation at a true 360px viewport: `scrollWidth == innerWidth == 360`, `overflow = 0`. `(max-width:480px)` matches and its computed values apply (`.ops-box` padding 20px vs 32px; `.ops-actions` flex-direction column). Required AUD-050's viewport tag to work. |
| AUD-028 | H Tests | Medium | No test covers any service business logic. The pure, database-free validation in `TicketService.CreateBooking` (traveller cap, duplicate names, duplicate seats) and in `AdminService.CreateEmployee` (role-specific required fields) is testable without a database and is untested. All 33 tests sit in `Security` and `Models`. | `BusNova.Tests/` contains only `PasswordHasherTests`, `PasswordPolicyTests`, `PasswordSetupTokenTests`, `RolesTests`, `TicketPricingTests` | Confirmed |
| AUD-029 | G Error handling | Medium | `customErrors` uses `redirectMode="ResponseRewrite"` with the status code passed as a query string (`~/Error.aspx?code=404`). Under `ResponseRewrite` the rewritten URL's query string is generally not visible to `Request.QueryString`, so `Error.aspx` would read `code` as null, fall to its `default` branch, and return **HTTP 500 with "Something went wrong"** for what was actually a 404. The 403 path is unaffected because `SessionHelper` issues a genuine `Response.Redirect`. | `Web.config:50-54` vs `Error.aspx.cs:24-52`; working 403 path at `Security/SessionHelper.cs:138` | **Partially settled** (2026-09-22) — `GET /NoSuchPage.aspx` returned **HTTP 404**, so the status code is correct and the predicted 500 did not occur. The response body came back empty over this client, so which message `Error.aspx` renders is still unconfirmed. Still **Suspected** on the message only. |
| AUD-030 | A Config | Medium | The database creation script was not executed, so "runs cleanly against a fresh SQL Express instance" is unverified. The live database proves the great majority of the schema exists and matches, with the one exception recorded as AUD-002. | `App_Data/BusNova_Database_Script.sql` | **Resolved** (2026-09-22) — the script was rewritten to target a scratch database (`BN_SchemaRef`) and run end to end via `sqlcmd`: exit code 0, no errors, success block printed. The app also boots and serves `Home.aspx` (HTTP 200). A full schema diff of the live database against that reference found exactly **one** structural difference, AUD-002; tables, columns and collation are identical. VER-004 closed. |
| AUD-031 | J Doc drift | Low | `SYSTEM_OVERVIEW.md` states the `RouteAssignment` filtered unique indexes "prevent double-booking a bus or driver in an overlapping slot". They prevent identical slots only (see AUD-013). | `SYSTEM_OVERVIEW.md:98` | Confirmed |
| AUD-032 | J Doc drift | Low | `SYSTEM_OVERVIEW.md` states `Ticket`'s "filtered unique indexes prevent double-booking a seat". In the live database that index is not filtered (see AUD-002). | `SYSTEM_OVERVIEW.md:102` | Confirmed |
| AUD-033 | J Doc drift | Low | `SYSTEM_OVERVIEW.md` states ticket and booking references are "generated inside transactions guarded against race conditions". The guarding is real, but the numbers are derived from `MAX(id)+1` rather than the identity value and have already diverged in practice (see AUD-004). | `SYSTEM_OVERVIEW.md:67` | Confirmed |
| AUD-034 | J Doc drift | Low | `SYSTEM_OVERVIEW.md` describes `IssueTicket.aspx` as able to "validate an existing ticket by number". It performs a lookup and displays the status; it does not validate, consume or record anything (see AUD-007). | `SYSTEM_OVERVIEW.md:45` vs `IssueTicket.aspx.cs:301-320` | Confirmed |
| AUD-035 | J Doc drift | Low | `SYSTEM_OVERVIEW.md` describes `MyTickets.aspx` as showing "ticket/booking history". It binds tickets only; `TicketService.GetBookingsForPassenger` and `GetBookingByReference` are never called from anywhere. | `SYSTEM_OVERVIEW.md:59` vs `MyTickets.aspx.cs:146`; dead methods at `Services/TicketService.cs:228` and `:219` | Confirmed |
| AUD-036 | J Doc drift | Low | The class comment on `Error.aspx.cs` says `App_Data` is blocked "by the requestFiltering rule in Web.config". There is no `requestFiltering` rule in `Web.config` — it was deliberately removed, as `Web.config:85-90` explains. The protection still works via the IIS default `hiddenSegments` list, so the behaviour is fine and only the comment is wrong. | `Error.aspx.cs:11-13` vs `Web.config:85-90` | Confirmed |
| AUD-037 | I UI/UX | Low | The seat map renders the driver position with `fa-steering-wheel`, which is a Font Awesome **Pro** glyph. The page loads the free 6.5.1 CDN build, so this icon has no glyph to render. | `Helpers/SeatMapRenderer.cs:55`; free-tier CDN link at `Site.Master:11` | Confirmed |
| AUD-038 | I UI/UX | Low | `Dashboard.StatusCss` maps every non-`Active` bus status to one style, so `UnderMaintenance` and `Retired` are visually identical on the manager dashboard. | `Dashboard.aspx.cs:216-221` | Confirmed |
| AUD-039 | D / Performance | Low | `Dashboard.BindManager` loads every ticket in the database into memory to count today's sales, and `BindPassenger` loads every assignment to show twenty. `IssueTicket.btnFindPassenger_Click` loads every passenger to match one contact string. | `Dashboard.aspx.cs:79-81`, `:138-144`; `IssueTicket.aspx.cs:143-145` | Confirmed |
| AUD-040 | Code health | Low | Four service methods are never called: `GetBookingsForPassenger`, `GetBookingByReference`, `GetMaintenanceForBus`, `GetAllFuelLogs`. The last is notable — it is exactly the query a fuel-efficiency report would need. | `Services/TicketService.cs:228, 219`; `Services/OperationsService.cs:229, 77` | Confirmed |
| AUD-041 | B Auth | Low | `SetPassword.Page_Load` unboxes `ViewState["PassengerId"]` with a hard `(int)` cast on every postback. A postback that arrives without that ViewState entry throws rather than showing the "invalid link" panel the page already has. | `SetPassword.aspx.cs:28-32` and `:47-48` | Confirmed |
| AUD-042 | H Tests | Low | `TicketPricingTests` contains a single test asserting the happy path. The fallback behaviour its own doc comment describes — missing, malformed or negative `BusNova:TicketPrice` — is untested. | `BusNova.Tests/TicketPricingTests.cs:15-19` vs `Services/TicketService.cs:254-263` | Confirmed |
| AUD-043 | A Config | Low | The `BusNovaDb` connection string hard-codes one developer's machine name (`LAPTOP-B5HU784A\SQLEXPRESS`). `README.md` step 3 does tell the reader to change it, so this is setup friction rather than a defect. | `Web.config:18`; `README.md:29` | Confirmed |
| AUD-044 | Data hygiene | Low | The live development database contains real personal email addresses in `dbo.Passenger` rather than test data. | Live `dbo.Passenger` rows (three non-seed addresses) | Confirmed |
| AUD-045 | D Workflows | Low | The payment page carries a permanently visible "Simulate a declined payment" checkbox. This is a deliberate, documented choice for demonstrability, recorded here only so the decision is visible when a real gateway replaces it. | `PaymentSimulation.aspx.cs:252-258`, `:362-366` | Confirmed |
| AUD-046 | C Security | Low | `AntiForgery.FixedTimeEquals` returns early on a length mismatch, so it is constant-time only for equal-length inputs. For a fixed-length base64 session token this leaks nothing useful; noted for completeness. | `Security/AntiForgery.cs:246-256` | Confirmed |
| AUD-047 | J Doc drift | Low | `README.md:34` points the reader at the `INSERT` statements for demo credentials, but the passwords are actually listed in the script's header comment block, and the sixth seeded Manager (`conductor@busnova.co.za` / `Conductor@2026`) is listed in neither the README nor the script's closing `PRINT` block. | `README.md:34` vs `App_Data/BusNova_Database_Script.sql:509-515` and `:646-657` | Confirmed |

### Addendum findings (discovered post-review, while scoping WP-0b)

| ID | Area | Severity | Description | Evidence (file:line) | Status |
|---|---|---|---|---|---|
| AUD-052 | D Workflows | Critical | `ManageUsers.aspx` does not compile and has never been loadable. Line 139 calls `Roles.Display(...)` in markup, where `Roles` resolves to `System.Web.Security.Roles` (auto-imported into every `.aspx`) rather than `BusNova_System.Models.Roles`, giving `CS0117: 'Roles' does not contain a definition for 'Display'`. A Manager therefore cannot open user administration at all: staff accounts cannot be created and accounts cannot be activated or deactivated - UC1 / UC21. `msbuild` reports nothing because `.aspx` markup is compiled on first request, not at build time. This explains the Stage 1 observation that no `EmployeeCreated` audit row exists: the page never rendered. Found when a `returnUrl` test navigated to the page; it is the only markup in the project that references `Roles.`. | `ManageUsers.aspx:139`; runtime `HttpCompileException` in `App_Data/Logs/errors-2026-09-22.log`; no commit has ever touched this file | **Resolved** (2026-09-22) — reference fully qualified; page renders and `returnUrl` to it now works |
| AUD-051 | I UI/UX | Low | At a 360px viewport the responsive table transform renders the stacked per-row cards correctly, but the GridView's original `<th>` header row is left visible above them and is clipped at the right edge ("Route / From-To / Bus / Date / De…"). It does not cause page-level horizontal scroll — the widest overflowing element is a `<th>` at 87px, contained by its wrapper — but it is a visible artefact on every table that uses the stacked-card pattern. | Observed under CDP device emulation at 360px on `BookTrip.aspx`; pattern defined in `Helpers/GridViewHelpers.cs` and `Styles/Ops.css:252` | Confirmed |
| AUD-048 | A Config / I UI | Medium | No `<globalization>` element exists in any config file, so `fileEncoding` is unset and the compiler reads BOM-less source as the system codepage (cp1252 on Windows). `AssignRoute.aspx` and `MyTickets.aspx` contain genuine UTF-8 em dashes and have no BOM, so their literals render as `â€"` at runtime. `Site.Master` has `<head runat="server">` with no `<meta charset="utf-8" />`. Proven by contrast: `Home.aspx` and `Login.aspx` also hold non-ASCII but **do** carry a BOM and render correctly. No mojibake exists in the source itself — this is purely a decode-time fault, so nothing needs retyping. | Absent from `Web.config`, `Web.Release.config`, `Web.Debug.config`; corrupted literals at `AssignRoute.aspx:129`, `AssignRoute.aspx:141`, `MyTickets.aspx:18`; missing meta tag at `Site.Master:6`; 103 of 114 source files carry no BOM | **Resolved** (FIX-014, 2026-09-21) |
| AUD-050 | I UI/UX | High | `Site.Master`'s `<head>` contains **no `<meta name="viewport">` tag**. Every mobile browser therefore renders the site at a default desktop viewport (typically 980px) and scales it down, so no CSS media query below that width ever activates. This means the 480px and 560px breakpoints that do exist in `Styles/Ops.css` and `Styles/Home.css` have never taken effect on a real phone, and it compounds AUD-026/AUD-027. Nothing responsive can work until this one line exists. Found while adding the charset meta tag for FIX-014; not fixed there because it is responsive-layout scope, not encoding scope. | `Site.Master:6-16` — `<head runat="server">` contains charset, title and four stylesheet links, and no viewport meta | **Resolved** (2026-09-22) — tag added; *runtime confirmation at 360px still outstanding* |
| AUD-049 | E Data integrity | High | The database contains **zero `nvarchar` columns** — all 49 text columns are `varchar`/`char` — and its collation is `Latin1_General_CI_AS`, a cp1252-based, non-supplementary-character collation. Roughly 22 of those columns hold user-entered text (`Passenger.FirstName`, `Ticket.TravellerName`, `Route.Origin`, `MaintenanceLog.IssueDescription`, `FuelLog.Station`, and others). Any character outside cp1252 — `Ānish`, `Māori`, any CJK name — is silently replaced with `?` **on insert** and is unrecoverable. Characters inside cp1252 such as `é` survive, which is why this has not yet been noticed. | Live `SELECT DATABASEPROPERTYEX('BusNovaDB','Collation')` → `Latin1_General_CI_AS`; live `sys.columns` → 49 `varchar`/`char`, 0 `nvarchar`; schema at `App_Data/BusNova_Database_Script.sql:126-445` | Confirmed |

### Unverified suspicions

Nothing was carried here. Every item above has either a file:line citation or a reproducible database observation. The four Suspected findings (AUD-012, AUD-027, AUD-029, AUD-030) each name the exact runtime action needed to settle them and are listed in the table rather than here, because each rests on concrete evidence of a *declaration* that was not observed *taking effect*.

---

## 3. Area narratives

### A — Build and boot

**Checked:** solution build, appSettings key reconciliation, database script and migration review, connection-string resolution, project-file completeness.

**Passed.** `msbuild "BusNova System.sln" /t:Restore,Build` completes with **0 errors and 0 warnings**. Both projects produce assemblies. All three documented `appSettings` keys are defined in `Web.config` and every one is read by code: `BusNova:LogDirectory` at `Global.asax.cs:108`, `BusNova:TicketPrice` at `Services/TicketService.cs:257`, `BusNova:ServiceIntervalMonths` at `Services/FleetService.cs:123`. No key is read but undefined, and none is defined but unread. Every file on disk under `Services/`, `Security/`, `Helpers/`, `Models/`, `DataAccess/`, `Scripts/` and `Styles/` is referenced by `BusNova System.csproj`, so nothing will be missing from a publish.

**Failed.** AUD-003 — the migration set cannot bring an existing database up to the current script, because no migration addresses the filtered seat index.

**Could not check.** Whether the creation script runs cleanly end-to-end on a fresh instance (AUD-030), and whether the app boots to `Home.aspx` without a configuration exception — the site was not deployed. The live database's structure matching the script almost everywhere is strong indirect evidence that the script does run.

### B — Authentication and session

**Checked:** seeded credentials, sign-in paths, `returnUrl` handling, logout, page guards, registration privilege boundary, password-setup lifecycle.

**Passed, and verified rather than assumed.** All seven seeded password hashes were recomputed offline with PBKDF2-HMAC-SHA256 at 100,000 iterations against the documented demo passwords, and **all seven matched** — including `conductor@busnova.co.za` / `Conductor@2026`, which no document lists. The seeded accounts will authenticate.

Page guards are complete. Every protected page calls `SessionHelper.RequireLogin` or `RequireRole` as the literal first statement of `Page_Load`: `AssignRoute:28`, `BookTrip:39`, `CreateRoute:22`, `Dashboard:28`, `IssueTicket:35`, `LogFuel:29`, `MaintenanceWork:25`, `ManageUsers:20`, `MyTickets:22`, `PaymentSimulation:52`, `RegisterBus:23`, `ReportIssue:24`, `TicketConfirmation:31`, `ViewAssignedRoutes:26`. The pages without a guard are the four that must not have one — `Home`, `Login`, `Register`, `Error` — plus `RequestPasswordLink` and `SetPassword`, which are reached by an unauthenticated passenger holding a token, and `Logout`, which only tears a session down. **No page is missing a guard it should have.**

`Register.aspx` is genuinely passenger-only: it calls `AuthService.RegisterPassenger`, which inserts into `dbo.Passenger` with no role parameter anywhere in its signature. There is no privilege-escalation path through it. Staff creation exists only in `AdminService.CreateEmployee`, behind a Manager guard, with the role validated against a three-element whitelist at `AdminService.cs:56-68`.

`returnUrl` is handled safely — `Login.aspx.cs` accepts it only when it starts with a single `/` and contains no colon, which closes the open-redirect hole. `Logout.aspx.cs` clears the session, abandons it, and expires the cookie.

The walk-up password-setup flow is correct by code inspection end to end: the token is 256-bit random, only its SHA-256 hash is stored, the consuming `UPDATE` carries `AND PasswordSetupTokenHash = @currentHash` so a replay between the read and the write fails closed, and expiry is checked. Both "no such account" and "account already has a password" return `null` from `TryFindPendingSetupPassenger`, so the reissue page cannot be used to enumerate addresses.

**Failed.** AUD-009 (session fixation), AUD-010 (no lockout), AUD-011 (three inconsistent password policies, weakest on staff accounts), AUD-021 (timing-based user enumeration), AUD-041 (ViewState cast).

On the specific question of whether the generic failure message leaks: the *message* does not — `GenericFailure` is returned identically for unknown email, wrong password, and deactivated account. The *timing* does (AUD-021).

**Could not check.** That the flow works in a browser. The audit log contains no `PasswordSetupCompleted` or `PasswordSetupLinkReissued` entries, so the feature appears never to have been exercised on this database.

### C — CSRF and security

**Checked:** token issuance and validation coverage handler by handler, cookie flags, headers, hashing, SQL construction.

**Passed.** The CSRF token is rendered once into a hidden field on the single shared `<form runat="server">` in `Site.Master:22`, refreshed per request at `Site.master.cs:44`, and **every state-changing postback handler in the application validates it.** Verified individually: `AssignRoute:74`, `BookTrip:144`, `CreateRoute:27`, `IssueTicket:175` and `:258`, `LogFuel:129`, `Login:52`, `MaintenanceWork:51` and `:103`, `ManageUsers:43`, `:99`, `:132`, `MyTickets:40`, `PaymentSimulation:115`, `Register:32`, `RegisterBus:28`, `ReportIssue:49`, `RequestPasswordLink:26`, `SetPassword:54`, `TicketConfirmation:75`, `ViewAssignedRoutes:42` and `:95`. The handlers that skip validation are all genuinely read-only — search, clear-form, dropdown rebinds, and `IssueTicket.btnValidate_Click`. **No state-changing handler skips validation.**

`PasswordHasher` is exactly what the documentation claims: PBKDF2, 100,000 iterations, SHA-256, 16-byte random salt per password, 32-byte output, constant-time byte comparison, and a malformed stored value returns `false` instead of throwing. Confirmed both by reading `Security/PasswordHasher.cs:25-110` and by independently reproducing the stored hashes.

**No SQL injection was found.** Every query runs through `DataAccess/Db.cs`, which offers no method accepting pre-concatenated values. The only string concatenation into command text is of compile-time constant fragments (`TicketSelect + " WHERE t.TicketID = @id;"`). User input reaches SQL exclusively through `SqlParameter`, including inside the hand-rolled transactional blocks in `TicketService`. The `LIKE '%' + @origin + '%'` search at `TicketService.cs:139-140` is parameterised and safe; user-supplied `%` characters are not escaped, which affects search precision but nothing else.

Cookies are `HttpOnly` in the base config, and `Web.Release.config:37` genuinely applies `requireSSL="true"` via an `xdt:Transform`, with the connection string replaced by a deliberately unusable placeholder.

**Failed.** AUD-012 (headers declared but emission unverified), AUD-022 (setup token in the URL), AUD-046 (minor).

### D — Core workflows

Walked in code. Runtime confirmation came from the audit log, which records which workflows have actually run.

| Workflow | Verdict |
|---|---|
| Manager registers a bus | Complete in code; two `BusRegistered` audit entries confirm it has run |
| Manager creates a route | Complete; two `RouteCreated` entries confirm |
| Manager assigns bus + driver | Complete; two `RouteAssigned` entries confirm |
| Rejected when bus already booked | Application check present (`AssignmentService.cs:282`); DB backstop weaker than documented — AUD-013, AUD-014 |
| Rejected when driver booked / licence expired / date past | All three present and correct — `AssignmentService.cs:254-266, 289` |
| Driver views own schedule | Complete; driver id comes from session, never the request |
| Driver starts, then completes trip | Code correct and ownership-scoped in the `WHERE` clause; no date gating (AUD-016); no `TripStarted`/`TripCompleted` audit entries, so never exercised |
| Driver logs fuel, optionally tied to a trip | Code complete; not transactional (AUD-017); no `FuelLogged` audit entries, so never exercised |
| Driver reports a fault | Complete, and a Critical fault correctly takes the bus out of service |
| Maintenance: view, start, close | Complete; `MaintenanceStarted` and `MaintenanceCompleted` entries confirm it has run; the "another critical fault still open" guard at `OperationsService.cs:367-387` is a genuinely good piece of logic |
| Passenger self-registers | Complete; three `PassengerRegistered` entries confirm |
| Passenger searches and books 1–5 seats | **Broken under tampering** (AUD-005, AUD-006) and **never completed** (AUD-020) |
| Draft survives BookTrip → PaymentSimulation | Correct by design — server-side session, not a hidden field |
| Payment succeeds before any DB write | **Correct, and a real strength.** `BookTrip` writes nothing; `CreateBooking` is called only after the simulated payment passes, in one transaction |
| TicketConfirmation shows one e-ticket per traveller | Correct, with a proper ownership re-check at `TicketConfirmation.aspx.cs:45` |
| MyTickets lists history and cancels | Works; ownership enforced twice; cancellation window too loose (AUD-015); no refund policy (AUD-023) |
| Manager issues a counter ticket | Complete; six `TicketPurchased` entries confirm |
| Manager validates a ticket by number | **Lookup only — does not validate** (AUD-007) |
| Manager creates staff / deactivates accounts | Code complete; weakest password rule applies (AUD-011); no `EmployeeCreated` audit entries, so never exercised |

Two access-control checks deserve explicit credit: `TicketService.CancelTicket` puts `PassengerID` in the `WHERE` clause as well as checking it in code, and `AssignmentService.StartTrip`/`CompleteTrip` put `EmployeeID` in the `WHERE` clause. Neither can be defeated by a tampered command argument. `TicketConfirmation` correctly refuses another passenger's `bookingId` with an indistinguishable message.

### E — Data integrity

**Checked against the live database, not just the script.**

`UQ_Assignment_BusSlot` and `UQ_Assignment_DriverSlot` both exist, are unique, and are correctly filtered on `TripStatus <> 'Cancelled'` — but they constrain exact slots, not overlaps (AUD-013). `UQ_Ticket_BookingTraveller` exists, is unique, and is correctly filtered on `BookingID IS NOT NULL`, so it does prevent a duplicate traveller name within a booking. `UQ_Ticket_AssignmentSeat` exists and is unique but is **not filtered** (AUD-002).

`CK_Employee_DriverFields` and `CK_Employee_MaintenanceFields` are both present and correctly formed. `CK_Employee_Role` allows exactly `Manager`, `Driver`, `MaintenanceStaff`, confirming migration 003 was applied. No foreign key or check constraint is disabled or untrusted, and no orphaned rows were found. Seven foreign keys lack a supporting index (AUD-019).

`BN-######` and `BNB-######` are generated inside transactions with an exclusive table lock, so the race the code describes is genuinely closed — but the number is taken from `MAX(id)+1` rather than the identity value, and the live data proves the two have already diverged (AUD-004). Seat identifiers exist in two mutually blind formats (AUD-008).

### F — Reporting

**This is the weakest area in the system.** There is no reporting page of any kind. `Dashboard.aspx` shows counters and three short grids, and that is the entirety of the analytical surface. See the gap analysis in section 4.

Dashboard numbers themselves are mostly right for each role, with the reservations in AUD-039: the manager tile counts are computed by pulling whole tables into memory. The manager overdue-service banner and the maintenance due/due-soon split are correct. There is no occupancy metric anywhere.

### G — Error handling

**Passed.** Unhandled exceptions genuinely are logged — `App_Data/Logs/errors-2026-09-08.log` exists and contains nine fully-formed entries with timestamp, request, user, type, message and stack trace. The logging path, the directory auto-creation, and the `HttpUnhandledException` unwrapping all work as described. Worth noting: every historical entry is the same `HttpParseException` from `GET /BuyTicket.aspx`, a page that no longer exists, caused by `data-role` on an `asp:HiddenField` (which does not implement `IAttributeAccessor`). The current code avoids this by using `<input type="hidden" runat="server">` instead — so that defect is genuinely fixed, not merely hidden.

`AuditService` does swallow failures without breaking the caller, exactly as claimed, in both `Write` and `WriteLogin`. There are only six bare `catch` blocks in the entire codebase — two in `AuditService` and four in `Global.asax.cs` — and every one is deliberate, documented, and in a place where throwing would replace a handled error with an unhandled one. **No bare catch hides a real error in business logic.**

**Failed.** AUD-029 — the 404/500 routing through `customErrors` is likely returning the wrong status and the wrong message.

### H — Tests

**Passed.** `BusNova.Tests` builds and **all 33 tests pass**, run via `vstest.console.exe`. All five claimed coverage areas exist and are real: password hashing (7 tests), password policy and strength scoring (13), setup tokens (9), roles (3), ticket pricing (1). No test is skipped, commented out, or marked `[Ignore]`, and every test contains at least one meaningful assertion. The hashing and token tests are genuinely good — they cover malformed input, empty input, salt uniqueness, and the exact-expiry boundary.

**Failed.** AUD-028 (no service-logic coverage at all) and AUD-042 (pricing fallback untested). The 33 tests sit entirely in `Security` and `Models`; not one line of `Services/` is executed by the suite.

### I — UI/UX baseline

**Passed.** Every page uses `Site.Master` except `Logout.aspx`, which renders nothing and immediately redirects — appropriate. **No emoji** appear anywhere in the markup or code-behind. Icons are Font Awesome and Bootstrap Icons classes throughout, with the single exception in AUD-037.

Navigation visibility is correctly role-scoped and, importantly, **matches the server-side guards exactly**: the Manager block links to the five Manager-guarded pages, the Driver block to the three Driver-guarded pages, the Maintenance block to `MaintenanceWork.aspx`, the Passenger block to `BookTrip` and `MyTickets`. No nav item is visible to a role that cannot open it, and no accessible page is missing from its role's nav.

**Formatting is consistent**, which is better than the brief anticipated. Every currency value in the application uses `ToString("C", CultureInfo.GetCultureInfo("en-ZA"))` — eight call sites, no exceptions, no hand-rolled `"R "` prefixes. Every date uses `dd MMM yyyy`, whether through `ToString` in code-behind (13 sites) or `DataFormatString="{0:dd MMM yyyy}"` in markup (17 sites). Times are consistently rendered as zero-padded 24-hour `HH:mm`. No formatting inconsistency was found to report.

**Failed.** AUD-024 (64 inline styles), AUD-025 (three `confirm()` dialogs), AUD-026 (no breakpoints in the seat-map and base stylesheets), AUD-037, AUD-038.

**Could not check.** Actual rendering at 360px (AUD-027), keyboard navigability, and contrast ratios — all require a browser.

### J — Documentation drift

Eight drift items, all Low: AUD-031 through AUD-036, AUD-047, and the README test-coverage claim implicit in AUD-042. Full list in section 5.

The documentation is, overall, unusually honest — `SYSTEM_OVERVIEW.md:116` volunteers that no email or PDF capability exists, and `:122-127` accurately describes the uncommitted state of the tree. The drift that does exist clusters on database-guarantee claims that are stronger than the schema delivers.

---

## 4. Required reports gap analysis

Per the Stage 1 scoping decision, all reports are treated as in-scope promises.

### Project 6 brief — the four required reports

| Report | Status | Notes |
|---|---|---|
| Daily route completion summary | **Missing** | No page. `AssignmentService.GetByDate` exists and is documented as "used by the route-completion report" (`AssignmentService.cs:128`) — the query is there, the report is not. |
| Fuel efficiency trends | **Missing** | No page. `OperationsService.GetAllFuelLogs` exists, is the right query, and is **never called** (AUD-040). `FuelLog.AssignmentID` and `OdometerReading` were added specifically to make this computable. |
| Driver performance report | **Missing** | No page and no supporting query. |
| Maintenance due/overdue list | **Missing as a report.** Partially present as a dashboard panel — `Dashboard.BindMaintenance` shows overdue and due-within-14-days counts plus a grid, for the MaintenanceStaff role only. A Manager sees only an overdue count banner. Not a report: no date range, no export, no per-bus history. |

**Score: 0 of 4 implemented. 1 of 4 partially surfaced on a dashboard.**

### Sprint system matrix reports

| Report | Status |
|---|---|
| Fuel Efficiency Report | **Missing** |
| Driver Schedule Report | **Missing** — `ViewAssignedRoutes.aspx` shows a driver their own schedule; there is no manager-facing, printable or date-ranged schedule report |
| Maintenance Schedule Report | **Missing** — see the dashboard-panel note above |
| Route List Report | **Missing** — `Dashboard` shows a grid of active routes; no report, no export, no inactive routes |
| Ticket Sales Report | **Missing** — `TicketService.GetAllTickets` exists and is used only to compute one dashboard counter |

**Every named report is missing.** In several cases the underlying service query already exists and is either unused or used only to populate a single dashboard number — which means the gap is largely presentation, not data access. That is the good news inside a bad result.

No report anywhere in the system has an export action of any kind.

---

## 5. Documentation drift

| Source | Claim | Reality |
|---|---|---|
| `SYSTEM_OVERVIEW.md:98` | `RouteAssignment` filtered unique indexes "prevent double-booking a bus or driver in an overlapping slot" | They prevent identical `(resource, date, departure time)` rows only. A unique index cannot express overlap. — AUD-013 |
| `SYSTEM_OVERVIEW.md:102` | `Ticket` "filtered unique indexes prevent double-booking a seat" | The live index is unfiltered, which over-blocks rather than under-blocks: cancelled seats stay dead forever. — AUD-002 |
| `SYSTEM_OVERVIEW.md:67` | Ticket/booking references "generated inside transactions guarded against race conditions" | Guarding is real; the values are derived from `MAX(id)+1`, not the identity, and have already diverged. — AUD-004 |
| `SYSTEM_OVERVIEW.md:45` | `IssueTicket.aspx` can "validate an existing ticket by number" | It displays the ticket's status and changes nothing. No ticket is ever marked `Used`. — AUD-007 |
| `SYSTEM_OVERVIEW.md:59` | `MyTickets.aspx` shows "own ticket/booking history" | Tickets only. The two booking-lookup service methods are dead code. — AUD-035 |
| `SYSTEM_OVERVIEW.md:105` | The three migration scripts "apply the same schema evolution to an existing database without a full recreate" | They do not. Script revision 12 — the filtered seat index — has no migration. — AUD-003 |
| `SYSTEM_OVERVIEW.md:24` | "Every protected page enforces its own role check server-side … as the first statement in `Page_Load`" | **Accurate.** Verified page by page. |
| `SYSTEM_OVERVIEW.md:76` | `PasswordHasher` — "PBKDF2-SHA256, 100,000 iterations, random salt, constant-time verification" | **Accurate**, and independently verified against the seeded hashes. |
| `SYSTEM_OVERVIEW.md:79` | AntiForgery "checked on state-changing postbacks" | **Accurate.** Every one. |
| `SYSTEM_OVERVIEW.md:9` | "raw parameterized SQL" | **Accurate.** No concatenated user input anywhere. |
| `Error.aspx.cs:11-13` | `App_Data` blocked "by the requestFiltering rule in Web.config" | No such rule exists; `Web.config:85-90` explains it was removed. Behaviour is still correct via IIS defaults. — AUD-036 |
| `README.md:34` | Demo logins — "see the `INSERT` statements … for the seeded usernames/emails" | Passwords are in the header comment, not the `INSERT`s, and the sixth Manager account is documented nowhere. — AUD-047 |
| `README.md:56` | Coverage includes "ticket pricing" | One happy-path assertion; the documented fallback contract is untested. — AUD-042 |
| `README.md:38-44` | The three `appSettings` keys and their purposes | **Accurate.** All three defined, all three read. |
| `README.md:46` | `Web.Release.config` "enforces HTTPS-only cookies" | **Accurate.** The transform is real and correctly targeted. |

---

## 6. What was verified as working

Recorded deliberately, so that Stage 2 does not spend effort re-proving it:

- Clean build: 0 errors, 0 warnings.
- All 33 unit tests pass.
- All 7 seeded password hashes verify against their documented passwords.
- `PasswordHasher` is PBKDF2-SHA256 at 100,000 iterations with constant-time comparison.
- Every protected page has its guard as the first statement of `Page_Load`.
- Every state-changing postback handler validates the CSRF token.
- No SQL injection vector exists; all user input is parameterised.
- `returnUrl` cannot be used as an open redirect.
- `Register.aspx` cannot create a staff account.
- The password-setup token is single-use and replay-safe at the SQL level.
- Ownership is enforced in the `WHERE` clause — not merely in code — for ticket cancellation and for trip start/complete.
- Nothing is written to the database before the simulated payment succeeds.
- Unhandled exceptions are written to `App_Data/Logs`, with a log file on disk to prove it.
- `AuditService` failures cannot break the operation being audited.
- Currency and date formatting are consistent across every page.
- Navigation visibility matches the server-side role guards exactly.
- No emoji, no default-unmodified Bootstrap page, and every page but `Logout.aspx` uses the shared master.

---

## 7. Status legend

- **Confirmed** — reproduced, or proven by reading the code or by querying the live database.
- **Suspected** — consistent with the evidence, but requires a specific runtime action, named in the finding, to settle.
- **Resolved** — set during Stage 2 when the fix lands and its acceptance test passes.
