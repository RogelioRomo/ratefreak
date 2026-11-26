# CVE assessment and release policy

Effective 2026-11-26, following the owner's authorization to accept findings
whose assessed practical risk is low and to apply available fixes. This applies
to all CVEs encountered during development, release preparation and maintenance.
It supersedes the earlier requirement that every finding have a fix or complete
caller exclusion. Dated technical assessments retain their original evidence.

## Assessment and fixes

Review each finding against the shipped component, affected versions, supported
configuration, reachable inputs, attack prerequisites and potential impact.
Consider primary vendor/upstream assessments and available exploitation evidence.
Scanner severity is an input to this assessment, not an automatic release veto
or an automatic reason to ignore the finding. Lack of a demonstrated exploit
alone does not establish low risk.

Apply available, compatible vendor fixes as the default, including fixes for
low-risk findings. Verify the affected behavior and relevant application,
installation and recovery paths before adopting changed artifacts. A proposed
upstream patch is a candidate for evaluation, not a qualified vendor update.
If a listed fix cannot safely be adopted, record the compatibility or regression
reason and the resulting risk decision.

When practical risk is assessed as low, the owner authorizes documented risk
acceptance without requiring a custom runtime fork, platform migration or a new
permission request for each finding. Record the ID, affected artifacts, rationale,
remaining exposure, available-fix status and conditions requiring reassessment.
Keep accepted findings visible in inventories; do not relabel them as patched,
unaffected or proved unreachable.

Materially exploitable findings and findings without enough evidence to assess
their risk remain open for investigation, mitigation or remediation. This policy
does not authorize blanket suppression of all CVEs carrying a low scanner score.

## Reassessment

Revisit accepted findings during release/dependency reviews and when a compatible
vendor fix becomes available, upstream changes its assessment, exploitation
evidence emerges, or component versions, native code, configuration, topology or
reachable inputs change. Prefer a routine validated fix over carrying an exception.

Final release artifacts still require current dependency/image security review.
An acceptance bound to earlier images does not establish that changed artifacts
have no new actionable findings. Application security controls and privacy/secret
checks remain release requirements.

## Current accepted findings

CVE-2019-1010022 and CVE-2019-1010024 are accepted as low practical risk for the
reviewed reference installation. They weaken native exploitation defenses;
neither provides a demonstrated independent attack against RateFreak. Debian
classifies both as unimportant and reports them unfixed; Ubuntu assigns low
priority to 1010022 and follows upstream's assessment. The observed stack behavior
remains present, and a separate memory-corruption exploit could make it relevant.
The acceptance relies on that mechanism and deployment assessment together with
the vendor judgments, not merely the absence of a demonstrated attack.

Sources: [Debian 1010022](https://security-tracker.debian.org/tracker/CVE-2019-1010022),
[Debian 1010024](https://security-tracker.debian.org/tracker/CVE-2019-1010024),
[Ubuntu 1010022](https://ubuntu.com/security/CVE-2019-1010022).
The upstream TLS guard-page proposal for 1010022 remains a candidate, not an
applied fix: [proposal](https://sourceware.org/pipermail/libc-alpha/2026-May/177104.html).

The [acceptance record](release-evidence/2026-11-26-security-risk-acceptance/decision.json)
and [current dispositions](release-evidence/2026-11-26-security-risk-acceptance/dispositions.json)
bind the decision to the previously reviewed images/configuration. These two
findings no longer block the current security review. Provider clearance, final
artifact/security qualification, operational acceptance and publication remain
separate [release work](STATUS.md#remaining-release-gates).
