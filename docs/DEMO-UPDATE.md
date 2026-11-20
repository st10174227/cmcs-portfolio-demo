# Visitor demo and website polish — 20 November 2026

## Verified baseline

The user's personal .NET 10 build and 38 checks passed on 20 November 2026. The supplied account migration 20261120122247_AddPersonalAccounts was reviewed: Up creates seven Identity tables and eight indexes, with no edits/deletions to existing claim or lecturer rows. The uploaded snapshot preserves lecturer fields/seeds and claim relationships; claim Status/IsPaid have the intended concurrency tokens. The personal site started on port 5092. Browser captures show home, lecturer, coordinator and HR pages and a readable R5,000 invoice. They do not prove phone layout or every browser interaction.

## Changes made from the screenshots

- Claims now use explicit South African Rand amounts, a consistent page heading, restrained actions and a scrollable table with keyboard focus and meaningful empty states. Review status and payment status are distinct columns. Notes remain available in Details. The old inline stylesheet, stray closing brace and decorative emoji heading are removed.
- Details use a responsive table, clear date labels and a separate payment-state description. HR labels say approved rather than conflating coordinator verification with final approval. Coordinator confirmation explicitly verifies and forwards; manager confirmation approves.
- The invoice identifies the lecturer as payee, distinguishes claim status from UNPAID/PAID (recorded), uses Rand amounts, and labels the public demo as fictional. Recording a payment does not execute a bank transfer.
- Demo role cards, persistent switch/reset controls, screen-reader labels, dismiss labels and reduced-motion/keyboard behavior support the public walkthrough. A final desktop/phone and generated-PDF visual check is still required after this update runs.

## How the visitor demo works

Demo mode is an explicit, separate application mode. It is disabled for the personal launcher and enabled by Start-DemoWorkspace.ps1 on localhost port 5093. The personal sign-in on 5092 continues to use personal data and local passwords. Demo and personal apps use different auth, antiforgery, message and workspace cookies, Data Protection application names and data roots.

The public role-picker needs no password or registration. Role entry and reset require POST plus antiforgery validation. Only four fixed roles are accepted. A new visitor receives a protected, server-issued workspace cookie and an independent temporary SQLite file, sample lecturers, four passwordless demo identities and sample claims at each stage. No local credential text file is created. The demo entry endpoint is unavailable in personal mode. Switching roles keeps the visitor's workspace. The authentication ticket is tied to that workspace and is rejected with a mismatched, expired or retired workspace cookie. Staff permissions and lecturer ownership are still checked by the actual controllers.

Reset retires that visitor's workspace and signs them out, then choosing a role creates fresh samples. Other visitor workspaces are unaffected. Sessions expire after 45 minutes. A cleanup worker deletes retired/expired files once in-flight requests release their leases. Host restart invalidates all existing visitor session mappings. The demo intentionally keeps no durable user records.

This is a single-instance demo. The current host tracks at most 20 active workspaces, with 40 claims, 12 lecturer profiles and 10 MB of new attachments per workspace; each accepted document is limited to 5 MB. Mutating requests within a workspace are serialized to keep these limits consistent. Role entry has an IP rate limit, general demo traffic has an IP rate limit and ordinary in-flight HTTP requests are limited. SignalR receives the same account/workspace boundaries and has separate connection limits under Kestrel. General overload can return 429 or full workspace capacity can return 503. This is not a multi-instance production tenancy system or a promise of availability on free hosting.

Cleanup is best effort with logged retries. A process restart invalidates old sessions immediately; folders from older host instances are reclaimed separately after a two-hour grace period. Existing in-flight requests can finish before retired files are removed. Do not use a demo data folder containing personal files, a public web-root folder, or a persistent shared/multi-instance data mount. Use only fictional details and sample attachments.

## Validation status

The updated runner contains 55 groups: 17 core regressions, 22 personal HTTP groups and 16 demo HTTP groups. The demo tests exercise role entry, form tokens, independent browsers, mixed-cookie rejection, the real coordinator/manager/HR workflow, downloads, quotas, reset, expired-ticket rejection, cleanup, bounded allocation and personal-data isolation. The personal suite also checks that demo role endpoints cannot elevate a personal user.

The authoring environment has no .NET SDK or PowerShell runtime. C#/PowerShell grammar, JavaScript syntax, project XML, configuration syntax and patch checksums are validated here. All 55 user-PC runtime groups, browser layout, invoice appearance, Docker build and hosted acceptance are pending for this update. No new test result is claimed from static parsing.

## Local use in PowerShell

Apply the packaged update, which backs up source, stops only this personal apphost, runs the checker and starts the visitor demo if all checks pass. For subsequent launches:

```powershell
powershell -NoProfile -File .\scripts\Check-PersonalWorkspace.ps1
if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
powershell -NoProfile -File .\scripts\Start-DemoWorkspace.ps1
```

Open http://127.0.0.1:5093/Demo, choose Lecturer, Coordinator, Manager or HR, and use the banner to switch roles or reset. An incognito browser receives a separate workspace. The address is local to the user's PC; it is not a published link.

For the existing personal app instead, run scripts/Start-PersonalWorkspace.ps1 after stopping the demo if rebuilding. Both apphosts use the same build output, so stop them before applying code updates.

## Publishing preparation

Dockerfile.demo and render.yaml prepare a disposable ASP.NET Core demo deployment from a future personal repository. No repository or host has been created or changed by this update. The deployment still needs user-PC checks, visual acceptance and a Docker/host smoke test. See docs/DEPLOY-DEMO.md for PowerShell commands and hosting limits.
