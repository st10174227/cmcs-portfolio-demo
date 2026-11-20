# Demo uses the normal website layout — 20 November 2026

The preceding visitor-demo version built on the user PC, passed all 55 checks and started on port 5093. The supplied screenshot showed a separate role-picker landing page. The user requested the normal application layout for the demo.

The /Demo action now renders the same Views/Home/Index.cshtml used by /. The normal Welcome to CMCS hero, four illustrated workflow panels, coloured workspace cards, features, How It Works section and footer are shared. Both modes render each workspace card through one partial, using the existing blue/cyan/green/yellow themes and Access Dashboard action. In demo mode a card submits its fixed role through the existing antiforgery-protected POST endpoint. Personal mode continues to use account sign-in and its assigned-role navigation.

The separate demo welcome/walkthrough screen is replaced by this shared homepage. A small Demo badge and Demo options dropdown sit in the existing navigation bar; there is no additional full-width demo banner. The dropdown keeps the temporary-data notice, workspace expiry, role-switch link and reset form. Normal claim/review/HR pages remain shared by both modes. No storage, authentication, permission, quota, session or reset implementation changes are part of this layout update.

The existing demo landing check now verifies that both entry URLs contain the normal hero/workspace/features sections and that no separate demo banner is rendered. The suite remains 55 groups; no new test group is added for the presentation-only change. Static C#/PowerShell/JS syntax and patch hashes are checked in the authoring environment. The updated Razor build, runtime results and visual acceptance must run on the user PC, which has the .NET SDK. The authoring environment cannot run the .NET website.

Applying the package backs up changed source, stops only apphosts belonging to the personal project, runs the checker and starts the local demo if checks pass. Open http://127.0.0.1:5093/ for the normal homepage; /Demo uses the same view. This remains a local URL. Public repository/hosting verification is a later release step.
