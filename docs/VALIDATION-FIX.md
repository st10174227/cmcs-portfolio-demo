# Claim numeric-validation correction — 20 November 2026

## Confirmed failure

The user-PC authentication build and 24 checks passed. The diagnostic rerun showed HTTP 500 for HoursWorked=NaN: RangeAttribute selected an integer validator from [Range(1, 744)] on the double property and attempted Convert.ToInt32 before the controller could return its form errors. No actual personal-data migration or website startup was triggered by the failing regression runner.

## Correction

HoursWorked now uses [Range(1d, 200d)] so validation compares double values and matches the existing 1–200-hour business limit. Lecturer StandardHourlyRate uses an explicit decimal range from 0 to 1000, removing integer conversion/rounding at the boundary. The claim hourly-rate attribute also uses decimal limits. Server-derived owner/rate/payment fields, permissions and workflow remain enforced by the account update.

Two new regression groups exercise the actual model attributes against NaN, infinities, huge values, fractional hours, exact limits and decimal-rate overflow/rounding boundaries. The existing HTTP invalid-input group now checks both Create and Edit, requires visible form validation for HTTP 200 and verifies that the claim count and original claim remain unchanged. Diagnostic exception-only logging is retained in the isolated HTTP fixture. The runner now contains 38 checks; these new results are pending on the user PC.

The installer backs up changed source, preserves local edits and invokes the existing strict build/check script. It does not regenerate or remove the account migration. Only if checks pass does it launch the personal website, which applies the previously generated migration and provisions local demo accounts at its first Development startup. The existing personal-data backup was created by account initialization. Send the generated account migration ZIP back for source review before publication.

Authoring verification: C# and PowerShell grammar, project XML, manifest checksums and ZIP integrity only; this environment has no .NET SDK/PowerShell runtime. User-PC execution is still required. No test was removed or changed to accept server errors. An HTTP 500 remains a failure.

Reference: Microsoft RangeAttribute source documents distinct integer, double and type/string constructors: https://source.dot.net/System.ComponentModel.Annotations/System/ComponentModel/DataAnnotations/RangeAttribute.cs.html . The reported failure is from the user's installed 10.0.12 runtime; current online source may include later error-handling changes.
