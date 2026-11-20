# Personal account and claim-permission update

## What changes

The personal CMCS website adds ASP.NET Core Identity accounts, sign-in/sign-out, role-specific navigation and server-side permission checks. Passwords are handled by Identity rather than stored in application source. This remains a local development website; it is not a deployed portfolio release.

| Account role | Access |
| --- | --- |
| Lecturer | List/read their own claims and attachments; submit claims and edit/delete only their own claims awaiting coordinator review |
| Coordinator | Read claim records and documents; verify or reject coordinator-pending claims |
| Manager | Read claim records and documents; approve or reject manager-pending claims |
| HR | Read claims/documents; maintain lecturer records; export reports; record payment status only for approved unpaid claims |
| Signed out | Home, sign-in/error pages, public assets and health only |

Claim owner and hourly rate come from the signed-in lecturer profile. Posted owner, rate, status, document path and payment fields do not set those server-owned values. Coordinator approval, manager approval and payment recording are separate stages. GET approval links display confirmation pages; they do not change data. Payment recording does not transfer money.

Review/payment state fields use optimistic concurrency checks. Bulk operations save eligible claims individually and can partially complete if a later row conflicts. Existing `Submitted` claims remain eligible for coordinator review; old `Verified` claims require manager approval before HR payment recording. Existing claims are retained without assigning them to new demo users.

## Local setup and migration

Apply-AuthenticationUpdate.ps1 first checks source hashes and preserves local edits. It stops only the personal apphost matching this folder, backs up changed source outside the project, and applies the payload. It invokes Initialize-Authentication.ps1 to restore the local Microsoft dotnet-ef tool, build the account code and scaffold AddPersonalAccounts using the same Identity model options as runtime. The previous snapshot is backed up outside the workspace. EF packages/tool remain pinned to 10.0.12 to match the existing validated baseline.

Generated migration source is exported to `artifacts/CMCS-Account-Migration.zip`. The initializer refuses destructive/data-changing Up operations before startup. It then builds the generated migration and creates a consistent SQLite backup in `CMCS_POE/App_Data/Personal/Backups` if personal data exists. The regression runner applies migrations only to isolated temporary fixtures. The launcher migrates the actual personal file only after checks pass.

Send the generated migration ZIP back with build/check output so the generated source and snapshot can be reviewed and retained in the final source. It contains source code only, not accounts or personal data. If a step fails, share the error and do not manually run database update or remove existing migrations.

## Local accounts and private files

On the first Development startup through the personal launcher, five demo accounts are generated: two lecturers, coordinator, manager and HR. Their passwords are random and unique on that PC, written to `CMCS_POE/App_Data/Personal/LOCAL-ACCOUNTS-<timestamp>-<id>.txt`. Open this file locally. Do not paste it into chat, put it in screenshots or commit it. Startup leaves existing account passwords intact. The demo lecturer accounts use newly created synthetic profiles rather than taking ownership of old records. Outside Development, startup does not provision these accounts. There is no public registration, account administration, email verification/recovery or self-service password reset in this update; deployment needs an explicit account-provisioning and recovery design.

Authentication and antiforgery cookies have separate personal names. Password failures use account lockout and login requests have an IP rate limit. Unsafe MVC forms require antiforgery tokens. Every authenticated request rechecks the security stamp and active account/lecturer state; roles are refreshed. SignalR broadcasts select active recipient accounts belonging to the owner or staff roles; the old client-callable broadcast methods are removed.

New attachments accept PDF/DOCX/XLSX up to 5 MB with basic format checks, are protected with ASP.NET Data Protection and are saved outside the public web root. Downloads use a saved claim ID plus account access checks. This is not antivirus scanning or a full document parser. Public /uploads access is blocked. Legacy saved personal upload references remain readable through the guarded download action; the old encrypted files are retained, not imported from school. Cleanup of unreferenced new attachments is best effort with logging; legacy encryption is compatibility code, not a claim of modern encryption. Private data, keys, backups and credentials are excluded from Git. Keep the private data folder and keys together for recovery; Windows keys are protected for the local Windows account.

## Validation record

The preceding website-polish version built on the user PC and passed its 13 regression checks. The authentication update adds migration-preservation, concurrency and numeric-validation checks plus 21 in-process HTTP groups, for 38 checks in the updated runner. They cover sign-in cookies, antiforgery, anonymous and wrong-role denial, cross-lecturer reads/writes/downloads, forged server-owned fields, invalid numeric/month/document input, workflow order, repeated payment recording, HR reports, SignalR negotiation, account deactivation, lockout and sign-out.

At packaging time the authoring environment has no .NET SDK or PowerShell executable. C# and PowerShell syntax, JavaScript syntax, project XML, JSON and patch checksums are checked statically; these are not a successful .NET compilation or runtime result. Run the packaged installer on the user PC to obtain those results. Browser layout, keyboard/mobile acceptance and a manual two-account workflow remain separate checks. Persistent hosting, HTTPS/proxy configuration, dependency/runtime audit, account recovery, operational monitoring and publication are still pending.

## References

- Microsoft Identity configuration: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0
- Microsoft Identity model customization (including design-time options): https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0
- Original source provenance remains in PERSONAL_PROJECT_ORIGIN.md and docs/SOURCE-MANIFEST.json. Existing attribution comments are retained.
