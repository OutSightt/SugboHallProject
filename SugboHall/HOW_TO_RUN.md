# SugboHall (MVC + MySQL) - with Welcome page, Admin login and Admin dashboard

ASP.NET Core MVC + MySQL 8. Works in Visual Studio 2022 and 2026 (targets .NET 8).

## Pages
- `/`               Welcome (landing) page - public: venues, upcoming events, how booking works
- `/Admin/Login`    Admin login
- `/Admin`          Admin dashboard (after login)
- `/Bookings` `/Halls` `/Residents` `/Audit`   admin only (redirects to login if not signed in)

## Admin login (change it!)
Username: `admin`
Password: `Sugbo@2026`
Change both in `appsettings.json` under "Admin".

## 1. Create the database (MySQL Workbench)
File > Open SQL Script > `database/00_run_all.sql` > click the lightning bolt (Execute).
Refresh SCHEMAS: you should see `sugbo_events` with 4 tables.

## 2. Set your MySQL password
`appsettings.json` -> `Password=` (and the fallback line in `Program.cs`).

## 3. Run
Double-click `HallBook.sln`, wait for NuGet restore (online the first time), press F5.
Opens at http://localhost:5169

## Troubleshooting
- Welcome page shows a yellow notice: MySQL is not reachable or the database was not created yet.
- "Access denied": wrong MySQL password. "Unable to connect": start the MySQL80 service.
- Icons come from the internet; offline the app works but icons are missing.
