# Deploying the portfolio demo

The personal repository is published and the Render demo is at https://cmcs-portfolio-demo.onrender.com/. The hosted four-role workflow and an invoice download have been exercised; spreadsheet downloads and the remaining release checks still need acceptance. Use one running instance and ephemeral demo storage. GitHub Pages can host a portfolio/project showcase; it cannot execute this ASP.NET MVC application.

## Local validation

Run scripts/Check-PersonalWorkspace.ps1 and Start-DemoWorkspace.ps1. Inspect the demo on desktop and a narrow phone-width browser. Try a visitor in an incognito browser to confirm independent data. Test claim submission, each review role, payment recording, downloads and reset. Use fictional information only.

If Docker Desktop is running, a local image check uses PowerShell:

```powershell
docker build --file Dockerfile.demo --tag cmcs-demo:local .
if ($LASTEXITCODE -ne 0) { throw 'Docker build failed.' }
docker run --rm --name cmcs-portfolio-demo -p 127.0.0.1:5094:8080 -e ASPNETCORE_ENVIRONMENT=Development cmcs-demo:local
```

Open http://localhost:5094/ in a fresh browser session. This hostname separates its cookies from the native demo at 127.0.0.1; ports alone do not separate cookies. The Development flag here permits loopback HTTP for a local image check; a hosted service uses Production and HTTPS. Docker images exclude private data, uploads, credential files and build caches from their build context.

## Render preparation

The repository root contains render.yaml, which specifies a free Docker web service, the Dockerfile and /health. A deployment requires connecting the intended personal GitHub repository to Render and reviewing its service configuration before creation. It will obtain an onrender.com URL. No access to the user's Render account is assumed.

- Production demo mode uses /tmp/cmcs-demo. It never uses App_Data/Personal. All visitor data is disposable; a restart creates a fresh host session map.
- Render's HTTPS ingress forwards requests to the app. ASPNETCORE_FORWARDEDHEADERS_ENABLED=true is set in the Render definition for that managed ingress; this setting trusts forwarded proxy headers and must not be carried over blindly to a directly exposed server.
- AllowedHosts is restricted to cmcs-portfolio-demo.onrender.com and loopback. Add any custom hostname explicitly if one is configured later. The service uses HTTP port 8080 and /health for liveness.
- PORT=8080 tells Render the same port that ASPNETCORE_HTTP_PORTS binds, avoiding discovery of a different port during the initial deployment.
- ASPNETCORE_HTTPS_PORT=443 configures the HTTPS redirect destination; it does not create a TLS listener inside the container. Render terminates TLS at its edge. Its forwarded HTTPS requests reach the normal pages and keep Secure cookies. Unforwarded HTTP probes receive a 307 redirect before Razor generates antiforgery tokens. Render accepts 2xx or 3xx HTTP health responses. Do not change antiforgery SecurePolicy to allow insecure production forms.
- No production account password or local credential file belongs in the repo, Docker image or Render environment. The demo creates only synthetic passwordless identities through its controlled demo endpoint.
- The source currently pins EF/Identity packages to the previously validated .NET 10 baseline. Before publication, run the package audit below and resolve findings. Container base images follow .NET 10 patch updates; test the built image rather than assuming it matches the Windows environment.

```powershell
dotnet list .\CMCS_POE\CMCS_POE.csproj package --vulnerable --include-transitive
if ($LASTEXITCODE -ne 0) { throw 'Dependency audit command failed.' }
```

Review the findings printed by this command; its exit code alone is not proof that no vulnerabilities were reported.

## Production container regression check

The original container had no explicit HTTPS redirect port. Its logs showed unforwarded HTTP requests reaching a secure Razor form and raising an antiforgery SSL configuration exception. The public HTTPS workflow could still work through Render's forwarding ingress, so a Development-only local image check did not expose this case.

After building cmcs-demo:local, run the following in PowerShell:

```powershell
powershell -NoProfile -File .\scripts\Test-ProductionContainer.ps1 -Image cmcs-demo:local
```

This starts a temporary container on a loopback-only random port using Production defaults. It checks redirects for unforwarded HTTP requests, then simulates Render's forwarded HTTPS requests and checks role forms, Secure antiforgery cookies and /health. The container is stopped afterward. The same check runs in GitHub Actions after the Docker build. A passing CI check is separate from verification of the newly deployed Render revision.

## Free-hosting behavior and hosted acceptance

Render's free service sleeps after 15 idle minutes and may take about a minute to wake. Its local SQLite files, uploads and keys are lost on restart, redeploy or sleep. This design makes resets expected, rather than promising permanent claim storage. Free capacity and usage limits still apply. A hosted smoke test must verify HTTPS, secure cookies, /health, password-free role entry, separate incognito data, the complete review/payment flow, downloaded PDFs/spreadsheets, reset and recovery after a restart.

After hosted acceptance, put Live Demo and Source Code links in the repository README, keep LinkedIn updates on hold until all selected projects are polished. Preserve project-origin attribution. CV work remains outside this release task.

## Primary references checked on 20 November 2026

- GitHub Pages static hosting: https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages
- Render Docker/.NET support: https://render.com/docs/docker and https://render.com/docs/language-support
- Render free-plan behavior: https://render.com/docs/free
- Microsoft proxy header configuration: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0

