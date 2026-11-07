# BusNova System — System Credentials Directory

This document lists all system login accounts across all roles (Manager, Driver, Maintenance Staff, and Passenger) available in the BusNova database (`BusNovaDB`).

> **Note on Passwords:** Passwords are hashed using PBKDF2-SHA256 (100,000 iterations) via `BusNova_System.Security.PasswordHasher`.

---

## 1. Manager Accounts

| Role | Name | Username | Email | Password | Capabilities |
|---|---|---|---|---|---|
| **Manager / Admin** | Nomsa Radebe | `admin` | `admin@busnova.co.za` | `Admin@2026` | Full admin privileges, Manage Users, Register Bus, Create/Assign Routes, Counter Ticket Issuance, Manage Maintenance, Maintenance Records (view only), Driver Issues, Reports |
| **Manager** | Buhle Zulu | `bzulu` | `fmanager@busnova.co.za` | `Manager@2026` | Fleet management, Bus/Route registration, Scheduling, Counter sales, Maintenance management |
| **Manager** | Lindiwe Mqhayi | `lmqhayi` | `conductor@busnova.co.za` | `Conductor@2026` | Counter ticket issuance, ticket validation, passenger manifest, route assignments |

---

## 2. Driver Accounts

| Role | Name | Username | Email | Password | Licence # | Capabilities |
|---|---|---|---|---|---|---|
| **Driver** | Thando Mahlangu | `tmahlangu` | `driver1@busnova.co.za` | `Driver@2026` | `EC1234567` | View assigned schedule, Driver Workspace, start/complete trips, log fuel, report bus issues, passenger manifest |
| **Driver** | Sipho Dlamini | `sdlamini` | `driver2@busnova.co.za` | `Driver@2026` | `EC7654321` | View assigned schedule, Driver Workspace, start/complete trips, log fuel, report bus issues, passenger manifest |

---

## 3. Maintenance Staff Accounts

| Role | Name | Username | Email | Password | Specialisation | Capabilities |
|---|---|---|---|---|---|---|
| **Maintenance Staff** | Ashley Witbooi | `awitbooi` | `maint@busnova.co.za` | `Maint@2026` | Diesel engines & brakes | View open/completed maintenance work orders, start work, log completed service & parts, view Maintenance Due & Schedule reports |

---

## 4. Passenger Accounts

### Primary Demo Passenger
| Name | Email | Password | Student Number | Phone |
|---|---|---|---|---|
| Luzuko Pantsi | `passenger@busnova.co.za` | `Passenger@2026` | `2024-PASS-001` | `0821110005` |

### Newly Seeded Registered Passengers (10 Dataset Users)
All newly seeded passengers can sign in using **Email** and Password: **`Passenger@2026`**.

| # | Name | Email | Password | Student Number | Phone | Registration Date |
|---|---|---|---|---|---|---|
| 1 | Sipho Nkosi | `sipho.nkosi@example.co.za` | `Passenger@2026` | `STU2026001` | `+27823456789` | 30 days ago |
| 2 | Nomvula Zulu | `nomvula.zulu@example.co.za` | `Passenger@2026` | `STU2026002` | `+27834567890` | 28 days ago |
| 3 | Kagan Naidoo | `kagan.naidoo@example.co.za` | `Passenger@2026` | `STU2026003` | `+27845678901` | 25 days ago |
| 4 | Thabo Mokoena | `thabo.mokoena@example.co.za` | `Passenger@2026` | `STU2026004` | `+27856789012` | 22 days ago |
| 5 | Zintle Dlamini | `zintle.dlamini@example.co.za` | `Passenger@2026` | `STU2026005` | `+27867890123` | 20 days ago |
| 6 | Ayanda Botha | `ayanda.botha@example.co.za` | `Passenger@2026` | `STU2026006` | `+27878901234` | 18 days ago |
| 7 | Lungile Khumalo | `lungile.khumalo@example.co.za` | `Passenger@2026` | `STU2026007` | `+27889012345` | 15 days ago |
| 8 | Devan Pillay | `devan.pillay@example.co.za` | `Passenger@2026` | `STU2026008` | `+27890123456` | 12 days ago |
| 9 | Charl van der Merwe | `charl.vandermerwe@example.co.za` | `Passenger@2026` | `STU2026009` | `+27812345678` | 10 days ago |
| 10 | Busisiwe Sithole | `busisiwe.sithole@example.co.za` | `Passenger@2026` | `STU2026010` | `+27823456700` | 5 days ago |

---

## 5. Summary of Sign-in Endpoints
- **Sign In Page**: `http://localhost:<port>/Login.aspx`
- **Passenger Self-Registration**: `http://localhost:<port>/Register.aspx`
- **Walk-Up Password Setup**: `http://localhost:<port>/SetPassword.aspx`
