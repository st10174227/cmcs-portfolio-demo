# Website copy and navigation review — 19 November 2026

The supplied full-page capture, screencapture-127-0-0-1-5092-2026-11-19-22_18_02.pdf, shows the desktop homepage rendering with its navigation, hero, workspace cards, features, workflow and footer. The stray attribution text is no longer visible. Printed-page breaks in the capture are not treated as evidence of a browser layout problem. Mobile layout and account permissions are not established by this capture.

## Prepared corrections

- Keep the current CMCS name and desktop structure while personal branding remains undecided.
- Replace unverified speed, maximum-security and 100%-automation language with descriptions of the actual submission, review, record and reporting workflows.
- Make payment labels describe payment-status records; the application does not transfer money.
- Replace placeholder support contact details and dead footer links with working claim, review, lecturer, payment-history and report routes.
- Remove the notification navigation item and its repeated requests to the unimplemented Notifications controller. The controller/service source remains available for later integration. Existing claim-status SignalR page scripts are unchanged.
- Add a keyboard skip link and a label/state attributes for the mobile navigation toggle. Preserve visible error/success messages until dismissed.
- Move shared page behavior into site.js, avoid invalid hash-selector errors, and respect reduced-motion preferences.
- Correct the incomplete fade-in keyframes and remove the obsolete fixed-footer bottom margin. Version the local stylesheet/script URLs so the browser receives changed assets.
- Keep the academic source-attribution text in non-rendering Razor comments.

## Validation

The existing nine-page, two-spreadsheet and SignalR smoke checks passed before these copy/navigation edits. The previous source-comment update's running capture shows its visible-text fix took effect. Local JavaScript syntax and changed route references were checked for this update. The updated Razor build, regression checks and browser appearance remain pending on the user's PC.

The installer validates source hashes before changing files, backs up the affected files, stops only an executable whose full path matches the personal CMCS app, runs the existing strict build/checker, then starts the local site after success. No Git settings or school files are changed.

After applying, refresh the homepage and inspect the footer links, Quick Actions, keyboard skip link and mobile navigation. The existing Test-Website.ps1 can run in a second window. Sign-in, ownership/role controls, workflow guards, attachment handling and notification integration are still future functional work.
