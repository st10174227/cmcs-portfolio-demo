# CMCS — interactive claims workflow

A personal continuation of Mohammed Ba Yazed's university Contract Monthly Claim System. Built with ASP.NET Core MVC, Razor, Identity, Entity Framework Core, SQLite and SignalR.

Visitors can try the Lecturer, Coordinator, Manager and HR roles without creating an account. Each browser receives a separate temporary workspace with fictional sample claims. The demo uses the same pages and layout as the account-based application.

**Deployment status:** local browser and Docker operation have been confirmed by the project owner. Public hosting is the next step; there is no live demo link yet.

## What you can try

- Submit teaching hours and a supporting document as a lecturer.
- Verify a claim as a coordinator, then approve or reject it as a manager.
- Record an approved payment and export PDF invoices or Excel reports as HR.
- Switch roles to follow the same claim, or reset your own workspace.
- Open a separate private browser session to explore an independent workspace.

Payment recording is bookkeeping; the application does not transfer money. Use fictional information and sample files only. Sessions expire after 45 minutes and reset when the server restarts. This is a single-instance portfolio demo with disposable storage.

## Run locally with PowerShell

Install the .NET 10 SDK, then run from this repository:

```powershell
powershell -NoProfile -File .\scripts\Check-PersonalWorkspace.ps1
if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
powershell -NoProfile -File .\scripts\Start-DemoWorkspace.ps1
```

Open http://127.0.0.1:5093/. Choose a workspace card to enter a role; no password is required in demo mode. Demo options in the navigation allow role switching and reset. Press Ctrl+C to stop.

The reviewed account migration is included. A fresh demo checkout does not require generating a migration or providing an existing database.

### Docker

With Docker Desktop running:

```powershell
docker build --file Dockerfile.demo --tag cmcs-demo:local .
if ($LASTEXITCODE -ne 0) { throw 'Docker build failed.' }
docker run --rm --name cmcs-demo-local -p 127.0.0.1:5094:8080 -e ASPNETCORE_ENVIRONMENT=Development cmcs-demo:local
```

Open http://localhost:5094/ in a fresh browser session. The Development override permits local HTTP; public hosting uses the image's Production setting and HTTPS. Cookies and form tokens from an older container expire when its temporary keys are replaced. Reload a fresh page after a restart.

## Accounts and project structure

`scripts/Start-PersonalWorkspace.ps1` runs the normal account-based workspace at http://127.0.0.1:5092/. Its private account information, keys and data stay outside Git. Public demo mode creates synthetic identities and does not use personal accounts.

| Path | Purpose |
| --- | --- |
| `CMCS_POE/` | MVC website, Identity, workflow, reports and migrations |
| `CMCS_POE/Demo/` | Temporary browser workspaces, seed data and cleanup |
| `CMCS.PersonalChecks/` | Core, authorization and demo regression checks |
| `scripts/` | PowerShell launch and validation commands |
| `Dockerfile.demo`, `render.yaml` | Container and hosting configuration |

## Validation

The visitor-demo version passed 55 regression checks on the owner's Windows machine. The subsequent shared-layout update and local Docker demo were reported working by the owner. GitHub Actions is configured to build and run the regression suite on Windows and Linux, and separately build the Docker image. Those hosted checks must pass after publication; no hosted CI result is claimed in advance.

Checks cover migration compatibility, claim ownership, role authorization, antiforgery protection, workflow transitions, exports, independent visitor workspaces and demo reset. Mobile layout, hosted HTTPS and the full hosted workflow still require browser acceptance.

## Hosting and limitations

See [deployment instructions](docs/DEPLOY-DEMO.md) and the [release checklist](docs/RELEASE-CHECKLIST.md). This server application requires a backend host; GitHub Pages cannot run it. The Render template uses an ephemeral Docker service. Free hosting may sleep, and restarts discard demo data.

New attachments use ASP.NET Data Protection. The source retains an academic fixed-key AES helper solely for compatibility with older local attachments; that historical format is not suitable for sensitive data. No legacy files, private passwords, uploaded documents or encryption keys are included in the repository.

## Origin and attribution

This is a separate personal continuation, with the original academic provenance recorded in [PERSONAL_PROJECT_ORIGIN.md](PERSONAL_PROJECT_ORIGIN.md) and `docs/SOURCE-MANIFEST.json`. Existing source-attribution comments and bundled third-party license files are retained. No new license grant is asserted for code whose licensing has not been established.
