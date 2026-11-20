# CMCS website source review

This is the historical first-pass source review. See FOUNDATION-UPDATE.md for the current changes and confirmed/pending validation status.

Reviewed: 19 November 2026. This review is based on the uploaded source archive, not a running website. No .NET SDK or PowerShell runtime is available in the review session, so build and execution results are pending.

## Version comparison

| Version | Files | C# files | Razor views | What the source contains |
| --- | ---: | ---: | ---: | --- |
| CMCS_Part1 | 90 | 9 | 15 | Early MVC screens and basic models; several actions are placeholders. |
| CMCS_Part2 | 102 | 19 | 16 | File-backed claims, upload/encryption helpers, review actions and a separate xUnit project. |
| CMCS_POE | 135 | 45 | 23 | EF Core/SQLite, expanded claim forms, validation service, coordinator/manager reviews, HR screens and PDF/Excel services. |

Counts include bundled assets and, for POE, an academic presentation. Folder entries are excluded. The Part 2 test sources contain 51 Fact/Theory annotations across five files; this is an annotation count, not a passed-test count. Those tests reference the Part 2 project and have not been ported or run against POE.

POE is the strongest starting point by implemented feature coverage. Dates in file names are not evidence of a newer or working version, and no Git metadata was included.

## First corrections in this personal copy

| Area | Before | Included correction | Validation |
| --- | --- | --- | --- |
| Lecturer forms | HRController saved when ModelState was invalid and redisplayed valid forms. | Both form actions save only when ModelState is valid. | Four controller regression checks added; execution pending. |
| Lecturer model | Claims collection was uninitialized. | New lecturers start with an empty collection. | One regression check added; execution pending. |
| Claim identity | Count + 1 could reuse an ID still held by another claim after deletion. | Submission resets the posted identity to zero and lets SQLite allocate the key. | A real SQLite in-memory submission-after-deletion regression check added; execution pending. |
| Live updates | Pages connect to /claimHub, but Program did not map that endpoint. | Mapped the existing ClaimHub at that path. | Browser negotiation/update check pending. This does not complete notification integration. |

Only four existing application files are changed: Controllers/HRController.cs, Models/Lecturer.cs, Controllers/ClaimController.cs and Program.cs. The new checker and check project are separate files.

## Remaining findings

Evidence locations below refer to the original uploaded POE source. Subsequent edits can shift line numbers.

1. **Authentication and ownership are unfinished.** AuthorizeRoleAttribute reads session values and redirects to Account/Login, but there is no Account controller/view or authentication implementation in this export. Program does not configure session/authentication middleware, and the claim, coordinator, manager and HR controllers do not apply role/ownership checks. The website needs an actual account model, sign-in, role authorization and claim-owner scoping before public hosting.
2. **Review stages can be bypassed.** CoordinatorController.Verify (line 48) and ManagerController.Verify (line 53) change status without checking the expected incoming stage. Approval is triggered by GET links. BulkVerify similarly lacks a stage guard, and mutation actions do not consistently enforce antiforgery. Editing checks the status in the GET action but not in its POST action. Use a single server-side workflow policy and test forged/stale requests, repeated decisions, and paid/approved claims.
3. **Status vocabulary is inconsistent.** Examples include Verified, Pending Manager Approval, RejectedByManager and Rejected by Manager. Coordinator filtering and HR queries interpret them differently. Choose one vocabulary and a deliberate migration/compatibility policy for historical rows.
4. **Supporting documents need a storage redesign.** ClaimController.DownloadFile (line 187) combines a supplied filename with the upload directory without a containment check or ownership lookup. Attachments are kept under wwwroot/uploads. FileEncryption.cs uses fallback key/IV values and reuses the IV. Replace this with private storage, access-controlled downloads, per-file encryption metadata and safe replacement/cleanup. Do not reuse school upload data or keys.
5. **Notifications are disconnected.** NotificationsController is entirely commented out; notification services/repositories are not registered or called by the claim workflows. Mapping SignalR alone does not fix this. Notification persistence also uses a shared static collection and JSON file; integration and isolation need deliberate work.
6. **Form and model requirements differ.** Notes and navigation properties can be implicitly required by MVC even when a form does not supply them. The model allows up to 744 hours while ClaimValidationService limits a month to 200. Define the intended rules once and test actual HTTP model binding, optional notes/attachments and invalid lecturer IDs.
7. **UI has unfinished routes and screens.** Manager/Details is a placeholder. Some review and HR download links target the current controller instead of Claim/DownloadFile. The personal UI needs consistent navigation, error/empty states, responsive layouts and keyboard checks.
8. **Configuration was intentionally omitted.** Program expects DefaultConnection. A new personal connection, data directory, encryption key and ports must be configured before running the site. Source-only build and in-memory checks do not require the omitted school data.
9. **Namespace/model snapshot consistency needs care.** Runtime code mixes CMCS_Part2 and CMCS_POE namespaces, while migration snapshots still name older model types. The old namespace does exist through ErrorViewModel; this is not by itself proof of a compile failure. Review migration/model behavior before renaming technical types.
10. **Release checks are absent for POE.** No POE test project or active CI workflow was included. The new six-check suite is a start. Framework/dependency review, full HTTP integration tests, browser acceptance and CI remain publication tasks.

## Work order

1. Build this separate copy and execute the new regression checks. Capture actual errors before further changes.
2. Establish personal configuration/storage and review framework/dependency versions. Confirm fresh installation without school settings.
3. Implement sign-in, roles, claim ownership, workflow transitions, antiforgery and private attachments. Test normal and rejected requests through HTTP.
4. Finish notification integration, report behavior and incomplete screens. Test reports against saved state and after restart.
5. Select the personal name and polish the website. Check mobile layouts, keyboard navigation and the full lecturer → coordinator → manager → HR journey using demo data.
6. Add setup documentation, screenshots and CI; publish to a new personal GitHub repository after checks pass. Add the verified result to LinkedIn. Leave the CV for the user's separate plan.
