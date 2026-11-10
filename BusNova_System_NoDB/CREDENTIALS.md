# BusNova System — Login Credentials

Every login account in the BusNova database (`BusNovaDB`), for all four roles: Manager, Driver, Maintenance Staff and Passenger.

**Last checked: 2026-11-08.** Each password marked ✅ was tested against the hash stored in the database, using the same PBKDF2-SHA256 check the login page uses. Those passwords work on this laptop. They also work on any machine built from `DatabaseExport/BusNovaDB_FullScript.sql`, because that script copies the stored hashes unchanged.

> **How passwords are stored:** never as plain text. The `PasswordHash` column holds `PBKDF2$<iterations>$<salt>$<hash>` (see `Security/PasswordHasher.cs`). A password can be checked but never read back. If a password below says "set by the user", nobody can look it up, and it can only be **reset** (see section 6).

- **Staff** (Manager, Driver, Maintenance Staff) sign in with their **Username**.
- **Passengers** sign in with their **Email**.

---

## 1. Manager accounts

| Name | Username | Email | Password | Checked | Can do |
|---|---|---|---|---|---|
| Nomsa Radebe | `admin` | `admin@busnova.co.za` | `Admin@2026` | ✅ | Full admin: Manage Users, Register Bus, Create/Assign Routes, Counter Ticket Issuance, Manage Maintenance, Maintenance Records (view), Driver Issues, Reports |
| Buhle Zulu | `bzulu` | `fmanager@busnova.co.za` | `Manager@2026` | ✅ | Fleet management, bus/route registration, scheduling, counter sales, maintenance management |
| Lindiwe Mqhayi | `lmqhayi` | `conductor@busnova.co.za` | `Conductor@2026` | ✅ | Counter ticket issuance, ticket validation, passenger manifest, route assignments |

## 2. Driver accounts

| Name | Username | Email | Password | Checked | Licence # | Can do |
|---|---|---|---|---|---|---|
| Thando Mahlangu | `tmahlangu` | `driver1@busnova.co.za` | `Driver@2026` | ✅ | `EC1234567` | Assigned schedule, Driver Workspace, start/complete trips, log fuel, report bus issues, passenger manifest |
| Sipho Dlamini | `sdlamini` | `driver2@busnova.co.za` | `Driver@2026` | ✅ | `EC7654321` | Same as above |

## 3. Maintenance staff accounts

| Name | Username | Email | Password | Checked | Specialisation | Can do |
|---|---|---|---|---|---|---|
| Ashley Witbooi | `awitbooi` | `maint@busnova.co.za` | `Maint@2026` | ✅ | Diesel engines & brakes | Open/completed work orders, start work, log completed service & parts, Maintenance Due & Schedule reports |
| Andile Dlamini | `Andile` | `andiledlamini@gmail.com` | set by the user | — | — | Same as above |

## 4. Passenger accounts

### Demo passengers (password `Passenger@2026`)

| # | Name | Email (sign-in) | Password | Checked | Student Number | Phone | Registered |
|---|---|---|---|---|---|---|---|
| 1 | Luzuko Pantsi | `passenger@busnova.co.za` | `Passenger@2026` | ✅ | `2024-PASS-001` | `0821110005` | 2026-09-08 |
| 2 | Sipho Nkosi | `sipho.nkosi@example.co.za` | `Passenger@2026` | ✅ | `STU2026001` | `+27823456789` | 2026-10-08 |
| 3 | Nomvula Zulu | `nomvula.zulu@example.co.za` | `Passenger@2026` | ✅ | `STU2026002` | `+27834567890` | 2026-10-10 |
| 4 | Kagan Naidoo | `kagan.naidoo@example.co.za` | `Passenger@2026` | ✅ | `STU2026003` | `+27845678901` | 2026-10-13 |
| 5 | Thabo Mokoena | `thabo.mokoena@example.co.za` | `Passenger@2026` | ✅ | `STU2026004` | `+27856789012` | 2026-10-16 |
| 6 | Zintle Dlamini | `zintle.dlamini@example.co.za` | `Passenger@2026` | ✅ | `STU2026005` | `+27867890123` | 2026-10-18 |
| 7 | Ayanda Botha | `ayanda.botha@example.co.za` | `Passenger@2026` | ✅ | `STU2026006` | `+27878901234` | 2026-10-20 |
| 8 | Lungile Khumalo | `lungile.khumalo@example.co.za` | `Passenger@2026` | ✅ | `STU2026007` | `+27889012345` | 2026-10-23 |
| 9 | Devan Pillay | `devan.pillay@example.co.za` | `Passenger@2026` | ✅ | `STU2026008` | `+27890123456` | 2026-10-26 |
| 10 | Charl van der Merwe | `charl.vandermerwe@example.co.za` | `Passenger@2026` | ✅ | `STU2026009` | `+27812345678` | 2026-10-28 |
| 11 | Busisiwe Sithole | `busisiwe.sithole@example.co.za` | `Passenger@2026` | ✅ | `STU2026010` | `+27823456700` | 2026-11-02 |

### Self-registered passengers (chose their own password)

| Name | Email (sign-in) | Password | Registered |
|---|---|---|---|
| Yongama Goso | `goso.yonga@gmail.com` | set by the user | 2026-09-08 |
| Lolu Manya | `lulomanya@gmail.com` | set by the user | 2026-09-08 |
| Bahumi Motaung | `motaung@gmail.com` | set by the user | 2026-09-11 |
| Frag Test213414 | `frag213414@busnova.co.za` | set by the user (test account) | 2026-09-22 |

**Totals:** 7 staff accounts, 15 passenger accounts. All are active.

---

## 5. Moving the system to another laptop

1. Copy `DatabaseExport/BusNovaDB_FullScript.sql` to the other laptop. It runs on SQL Server 2012 or newer, any edition.
2. Open it in SQL Server Management Studio and click **Execute**. Or run:
   ```bash
   sqlcmd -S .\SQLEXPRESS -E -I -i BusNovaDB_FullScript.sql
   ```
   If a database called `BusNovaDB` already exists there, the script stops without changing anything. Rename or delete the old one first.
3. In `Web.config`, change `Data Source=LAPTOP-B5HU784A\SQLEXPRESS` to the new laptop's SQL Server name.
4. Sign in with any ✅ account above.

## 6. Resetting a password nobody knows

The app has no reset screen. (`SetPassword.aspx` only works with the one-time link printed when a walk-up passenger is registered at the counter.) To reset a password, make a new hash in the app's format and store it.

1. In PowerShell, make the hash. Change `NewPass@2026` to the password you want (it must meet the app's password rules):
   ```powershell
   $pw='NewPass@2026'; $salt=New-Object byte[] 16; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt); $k=New-Object Security.Cryptography.Rfc2898DeriveBytes($pw,$salt,100000,[Security.Cryptography.HashAlgorithmName]::SHA256); 'PBKDF2$100000$'+[Convert]::ToBase64String($salt)+'$'+[Convert]::ToBase64String($k.GetBytes(32))
   ```
2. In SSMS, store the printed value against the account:
   ```sql
   -- staff
   UPDATE dbo.Employee  SET PasswordHash = '<printed value>' WHERE Username = 'Andile';
   -- passenger
   UPDATE dbo.Passenger SET PasswordHash = '<printed value>' WHERE Email = 'motaung@gmail.com';
   ```

## 7. Sign-in pages

- **Sign in:** `http://localhost:<port>/Login.aspx`
- **Passenger self-registration:** `http://localhost:<port>/Register.aspx`
- **Walk-up password setup:** `http://localhost:<port>/SetPassword.aspx`
