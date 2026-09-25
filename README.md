# KMC Event Management SOC Solution - st20338692

A no-Bootstrap ASP.NET Core 8 solution for the CSE5013 Service Oriented Computing case study.

## Projects
- `KmcEvents.Api` - REST API + EF Core + SQL Server + JWT authentication.
- `KmcEvents.Client` - ASP.NET Core MVC client using `.cshtml` views and consuming the API through `HttpClient`.

## Database
Database name: `DB_KMC_st20338692`

Default SQL Server Express connection string is in `KmcEvents.Api/appsettings.json`.

## First run (Visual Studio 2022)
1. Open `KMC_Event_SOC_st20338692.sln`.
2. Restore NuGet packages.
3. Configure both API and Client as multiple startup projects.
4. Run API first, then Client.
5. On first API start, EF Core `EnsureCreatedAsync()` creates `DB_KMC_st20338692` and all tables automatically.

For a migration-based submission, after the system is stable you can replace `EnsureCreatedAsync()` with `MigrateAsync()` and create an `InitialCreate` migration in Visual Studio.

## Seed admin
On first API startup, the system seeds:
- Email: `admin@kmc.lk`
- Password: `Admin@123`

Change this password before any real deployment.

## Important demo payment design
This project intentionally does **not** store CVV or a full raw card number. The demo validates a 16-digit number, then stores only the masked number/last four digits, card holder, expiry, payment reference and result. This better demonstrates secure coding and the assessment's data-protection criterion.
