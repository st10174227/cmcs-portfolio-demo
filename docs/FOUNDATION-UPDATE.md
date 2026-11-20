# Personal foundation update — 19 November 2026

## Confirmed baseline

The supplied PowerShell output confirms the first personal copy built successfully and all six first-pass regression checks passed. It reported 41 build warnings, including nullable-reference issues and NU1701 for the .NET Framework-only iTextSharp package. The website had not been started.

The reported PC has .NET SDK 10.0.401 and ASP.NET Core/.NET runtime 10.0.12, as well as older runtimes.

## Changes prepared

- Target both application and check project at net10.0. Pin EF Core and its SQLite provider to 10.0.12.
- Replace iTextSharp 5.5.13.4 with iTextSharp.LGPLv2.Core 3.8.8, which provides a .NET 10 build. Retain the existing PDF service API and add PDF-reader checks.
- Remove unused direct references to the SQL Server provider, EF tools, SignalR client, old Session package and BCrypt. ASP.NET Core's server services are provided by the web framework; sign-in implementation remains a future step.
- Give required input strings explicit empty defaults. Mark optional claim notes/navigation and missing repository lookups as nullable. Existing required-field attributes remain in force.
- Remove unused exception variables and handle absent warning collections in review views. Keep PDF cells safe when a saved lecturer name is absent.
- Align migration metadata's entity names with the actual CMCS_POE models and its snapshot with optional Notes. The new migration check must verify this alignment before startup is accepted; no pending-model warning is suppressed.
- Create personal storage under CMCS_POE/App_Data/Personal/cmcs-personal.db. Startup does not read the academic DefaultConnection string. CMCS_PERSONAL_DATA_DIR is an explicit override for fixtures/other personal locations; the supplied launcher always sets the personal path.
- Add a loopback-only PowerShell launcher and development profile at http://127.0.0.1:5092. Environment values are restored after the foreground process stops. The launcher does not kill another process or change Git settings.
- Add a small /health endpoint and a separate website smoke script. HTTPS redirection remains enabled outside Development.
- Make the checker rebuild with warnings treated as errors. This verifies the cleanup rather than hiding warnings.

## Validation status

Only the previous six-check build is confirmed by the supplied output. This update has undergone local source/XML/patch validation, but its .NET build, 13-check execution, migration behavior and browser acceptance are pending on the user's PC. The 13 checks cover the previous six cases plus fresh migrations/model alignment, reopening a file-backed fixture, three PDF cases and two spreadsheet cases.

Run the checker after applying the patch. It uses isolated in-memory or temporary-file fixtures, not the website's personal data file. If a check fails, share its output before starting the website.

After the checker passes, start the site with scripts/Start-PersonalWorkspace.ps1. Once it is listening, open http://127.0.0.1:5092. An optional scripts/Test-Website.ps1 can run in a second PowerShell window; it reads the nine main screens, two spreadsheet endpoints and SignalR negotiation. It does not establish form, account or workflow acceptance.

## Work still required

Authentication/role and owner checks, controlled workflow transitions, antiforgery consistency, private attachment encryption/storage, notification integration, incomplete screens, personal branding and full browser acceptance remain unfinished. The site is currently a local prototype. There is no new GitHub publication or CV edit in this update.

## Dependency sources

- [EF Core SQLite 10.0.12 package metadata](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/10.0.12)
- [iTextSharp.LGPLv2.Core 3.8.8 package metadata](https://packages.nuget.org/packages/iTextSharp.LGPLv2.Core/3.8.8)
- [PDF library maintainer's source and usage notes](https://github.com/VahidN/iTextSharp.LGPLv2.Core)

The replacement PDF package is the maintainer's port of iTextSharp 4.1.6 and carries LGPL-2.0-only metadata. Keep the package's notices with the project. Its basic-font PDF coverage here does not establish full Unicode shaping or Linux native-font support.
