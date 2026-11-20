# CMCS publication and hosted acceptance

## Current evidence — 20 November 2026

- Account/validation version: 38 checks passed on the owner's Windows PC.
- Visitor-demo version: 55 checks passed on the owner's Windows PC.
- Shared-layout update: owner reports the demo working with the normal UI.
- Linux Docker image: owner supplied a successful build log and subsequently confirmed browser operation.
- Public repository, GitHub Actions results and hosted acceptance: not yet confirmed.

## Publish the personal source

The publication kit copies only listed source files from the tested personal workspace to a new sibling folder. It never changes the original workspace or an academic Git remote. It validates file contents, builds and runs the checks in the copy, and publishes only when explicitly invoked with -Publish.

The repository target is st10174227/cmcs-portfolio-demo. The script requires Git, .NET 10, and GitHub CLI signed in as st10174227. If GitHub CLI is missing, install it with `winget install --id GitHub.cli --exact`, open a new PowerShell window, then use `gh auth login`. Never paste a token or local account password into a chat.

After publication, inspect the Actions tab. Both operating-system checks and the container build must pass. If a publication attempt is interrupted, the generated folder is printed; its scripts/Publish-PersonalRepository.ps1 can retry without creating a different repository. The script never force-pushes or replaces an unrelated existing repository.

## Host and accept the demo

1. Connect this personal repository to a Docker-capable host. The included render.yaml can create a Render Blueprint service; review its plan and configuration before creating it.
2. Set AllowedHosts to the actual assigned hostname. Keep Production, CMCS_DEMO_MODE=true, /tmp/cmcs-demo storage and /health. Use one instance and HTTPS. Forwarded-header trust belongs only behind the managed ingress described in DEPLOY-DEMO.md.
3. Verify /health and the homepage over HTTPS. Check secure cookies, role entry without a password and the shared normal UI.
4. Submit a fictional claim, verify it, approve it, record payment, and open the PDF and Excel downloads.
5. In another private browser session confirm that claims and reset are independent. Check narrow-screen layout, role switching, expiry and behavior after restart.
6. Add the verified Live Demo URL to README. GitHub publication alone does not make the application accessible online.

LinkedIn updates are on hold until all selected projects have been polished. CV updates are also outside this release.
