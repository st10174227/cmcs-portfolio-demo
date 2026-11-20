# Personal project origin

This is a separate working copy of Mohammed Ba Yazed's Contract Monthly Claim System website, selected from the CMCS_POE export supplied on 19 November 2026.

- Original archive: CMCS-Source-20261119-212715.zip
- Archive SHA-256: 13b4a8b44fceb82efcf032fe6c44c915f10530517f390e2583038e4ee959c1f1
- Compared versions: CMCS_Part1, CMCS_Part2, CMCS_POE.
- Selected baseline: CMCS_POE because it includes the expanded claim, review and HR website workflows.
- The export contains no Git history, so its commit/branch identity and whether it is the latest version cannot be verified from the archive.
- Original source hashes are recorded in docs/SOURCE-MANIFEST.json. That manifest includes the academic presentation, which is omitted from this personal website copy.
- Local appsettings files, databases, upload folders and credentials were excluded by the source export. Those files were not received.
- No Account controller, Account views or authentication-service implementation were found in this export. The included authentication interface and authorization attribute are unfinished foundations.

The existing source attribution comments have been retained. The project is a work in progress. No new Git repository, remote, publication or CV change is part of this package.

## Foundation continuation

The first personal build and six regression checks passed on the user PC. A subsequent .NET 10, null-handling, PDF compatibility and local-launch update is described in docs/FOUNDATION-UPDATE.md. The later null-safety and website-polish versions built on the user PC and passed 13 regression checks. The original-source hash manifest remains unchanged as provenance.


## Personal accounts continuation — 20 November 2026

The separate personal source now includes Identity accounts and guarded claim/review/HR workflows, described in docs/AUTHENTICATION-UPDATE.md. The original export did not contain this implementation. This update needs a generated AddPersonalAccounts migration and user-PC build/runtime validation; the generated migration source must be returned for review. The original-source manifest remains unchanged. No academic repository, CV or published service is changed by this package.


## Visitor demo continuation — 20 November 2026

The reviewed account migration supplied from the user PC is included in this source. The account/validation version passed 38 checks and started successfully on that PC. The subsequent visitor-demo update adds independent disposable workspaces, role-entry controls, sample data, bounded storage and visual polish. Its expanded 55-check runner and hosted/browser acceptance remain pending. Full details are in docs/DEMO-UPDATE.md. This source continues to preserve academic provenance and source attribution; it does not publish to an academic repository.


## Publication preparation — 20 November 2026

The reviewed account migration is included. The visitor-demo version passed 55 checks on the owner's Windows PC. The owner subsequently confirmed the shared layout and local Docker demo working. The publication kit creates a separate source copy with README and CI configuration; no GitHub publication or hosted CI result is claimed until those actions complete. Earlier sections describe the state at their respective update stages. The original-source manifest and attribution comments remain unchanged. LinkedIn and CV changes are on hold.
