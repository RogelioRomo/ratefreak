# react-remove-scroll-bar 2.3.8 license provenance

Reviewed: 2026-11-17. Scope: exact npm distribution, publisher metadata,
upstream source ancestry and the checked-in supplemental license. No dependencies,
notices or release gates were changed during this investigation.

## Result

The exact `2.3.8` distribution explicitly declares MIT in both `package.json`
and README, but contains no license file. The existing supplement is authentic
upstream MIT text added later, in 2025. A concrete code-equivalence bridge now
connects it to the shipped code: every non-manifest file in the `2.3.8` tarball is
byte-identical to `2.3.7`, whose published source commit is the direct base of the
upstream license-only merge.

This supports retaining `2.3.8` and the supplemental upstream notice with this
qualified provenance. It does **not** establish that a license file existed in
the original tarball or at the `2.3.8` published gitHead. The latter commit is not
available from the public upstream GitHub API/raw endpoints. No older copyright
year or publisher confirmation has been invented. This is technical provenance
evidence, not a legal opinion about retrospective licensing.

## Exact distribution evidence

Retrieved public artifacts from the npm registry using HTTPS, without executing
package code or install scripts:

- [Version metadata](https://registry.npmjs.org/react-remove-scroll-bar/2.3.8)
  names `MIT`, author Anton Korzunov and gitHead
  `b3b1287aad81def2e2ae707274b74531b61ddbaf`.
- [Exact 2.3.8 tarball](https://registry.npmjs.org/react-remove-scroll-bar/-/react-remove-scroll-bar-2.3.8.tgz)
  has 27 regular files. Its SHA-512 matches the registry integrity and the
  repository lockfile:
  `sha512-9r+yi9+mgU33AKcj6IbT9oRCO78WriSj6t/cF8DWBZJ9aOGPOTEDvdUDz1FwKim7QXWwmHqtdHnRJfhAxEG46Q==`.
- Tarball `package/package.json` declares `license: MIT` and author
  `Anton Korzunov <thekashey@gmail.com>`; `package/README.md` ends with an MIT
  license declaration. Neither supplies the full license grant/copyright notice.
- [Registry history](https://registry.npmjs.org/react-remove-scroll-bar) records
  `2.3.8` publication at `2024-12-15T02:08:28.658Z` and `2.3.7` at
  `2024-12-15T01:15:25.311Z`. At retrieval, `latest` remains `2.3.8`.

The version-addressed npm URLs identify release artifacts; the hashes below fix
the bytes actually reviewed. Registry metadata can change independently of a
tarball, so its hash is a retrieval fingerprint, not a claim of immutability.

## Source and license ancestry

1. Registry `2.3.7` metadata names source commit
   [`29e9fcd1eecf7d3b77a767941c4a57fe461fc1e4`](https://github.com/theKashey/react-remove-scroll-bar/commit/29e9fcd1eecf7d3b77a767941c4a57fe461fc1e4).
   Its immutable [package.json](https://raw.githubusercontent.com/theKashey/react-remove-scroll-bar/29e9fcd1eecf7d3b77a767941c4a57fe461fc1e4/package.json)
   identifies version `2.3.7` and MIT.
2. Commit [`7301c160fda44cb8cf2b9fdfde61efad35736196`](https://github.com/theKashey/react-remove-scroll-bar/commit/7301c160fda44cb8cf2b9fdfde61efad35736196),
   dated `2025-05-21T17:01:24Z`, has that source commit as its parent and adds
   only `LICENSE`.
3. Upstream merge [`8ca9ba5ea52de03308fe8ced94f7b159a44d28ff`](https://github.com/theKashey/react-remove-scroll-bar/commit/8ca9ba5ea52de03308fe8ced94f7b159a44d28ff)
   was authored by Anton Korzunov (`theKashey`) on `2025-05-26T00:29:27Z`
   and has the two commits above as parents. The immutable
   [comparison from 2.3.7 source](https://api.github.com/repos/theKashey/react-remove-scroll-bar/compare/29e9fcd1eecf7d3b77a767941c4a57fe461fc1e4...8ca9ba5ea52de03308fe8ced94f7b159a44d28ff)
   reports only `LICENSE` added: no runtime source modification.
4. The [license at its addition commit](https://raw.githubusercontent.com/theKashey/react-remove-scroll-bar/7301c160fda44cb8cf2b9fdfde61efad35736196/LICENSE)
   has the same SHA-256 as the existing
   [supplement](../deploy/compliance/supplements/remove-scroll-bar-LICENSE.txt)
   and the pinned merge license referenced by
   [sources.json](../deploy/compliance/supplements/sources.json). It names Anton
   Korzunov and copyright year 2025.
5. Comparison of the [2.3.7 npm tarball](https://registry.npmjs.org/react-remove-scroll-bar/-/react-remove-scroll-bar-2.3.7.tgz)
   with `2.3.8` finds identical file sets and identical contents for all 26 files
   other than `package/package.json`. The manifest changes only its version
   `2.3.7` → `2.3.8` and dependency `react-style-singleton` `^2.2.1` → `^2.2.2`.
   All distributed JS, declarations, constants manifest and README match exactly.

The source commit to npm relationship relies on npm's publisher-supplied gitHead;
this task did not reproduce the TypeScript build. Byte equivalence between npm
releases was verified directly, rather than inferred from release numbering.

## Unavailable exact published commit

These public routes returned missing-commit/file responses:

- [GitHub commit API](https://api.github.com/repos/theKashey/react-remove-scroll-bar/commits/b3b1287aad81def2e2ae707274b74531b61ddbaf):
  no commit found for the SHA.
- [Raw published manifest](https://raw.githubusercontent.com/theKashey/react-remove-scroll-bar/b3b1287aad81def2e2ae707274b74531b61ddbaf/package.json)
  and [raw published license](https://raw.githubusercontent.com/theKashey/react-remove-scroll-bar/b3b1287aad81def2e2ae707274b74531b61ddbaf/LICENSE):
  `404: Not Found`.

A raw LICENSE 404 alone would not prove its absence if the entire commit is
unavailable. Tarball inspection independently proves the distribution omission.
Do not describe the exact gitHead as inspected source.

## Verified artifact fingerprints

SHA-256 of bytes retrieved on the review date:

| Artifact | SHA-256 |
| --- | --- |
| npm 2.3.8 version JSON | `826c618ece09367334160c1ab3a272fd54f8f12e3b0a45f75b0f2660d285855a` |
| npm 2.3.8 tarball | `ccc872d7a2dc007cbf9d755f30d56b8d80eabdbe22c44c95a709129bfdc46f01` |
| npm 2.3.7 tarball | `5ee31e784254a1a0c80b2a14a569c3dc254af4c75d709a04f79ff409e856df91` |
| Upstream added LICENSE / existing supplement | `a79aae0c0f21990d9d963bb3c5a79cdcea9a46f8523ba55c58d7fe776b6ebc84` |
| 2.3.7 source package.json | `484ad46c8cc261e168d7e2602bbb70b80f8d10307b7de91d99485da28c350a03` |
| GitHub immutable comparison JSON | `3ef57a4eba40811d60570a377d6c295658f3d1955d7cea96a950f847677327c6` |

GitHub API JSON fingerprints include representation metadata and may vary on a
later fetch; source commit/blob links remain the durable identity. Temporary local
downloads are evidence inputs, not guaranteed durable release artifacts.

## Dependency alternatives and recommended disposition

The lockfile resolves `@radix-ui/react-select@2.4.1` through `react-remove-scroll@2.7.2`
to `react-remove-scroll-bar@2.3.8`, alongside React 19.3.0. The selected scrollbar
package declares React 16.8/17/18/19 compatibility and `react-style-singleton ^2.2.2`.

No newer npm release exists in the retrieved registry history. Downgrading to
`2.3.7` would preserve identical runtime files while changing the manifest's
singleton range, and its tarball also omits LICENSE; it does not fix notice
packaging. No compatible published alternative containing a license was established.
A Git-source substitution would require its own build, lockfile and distribution
verification and is unnecessary to obtain the already-authenticated notice.

Recommended next edit, subject to project review: retain the current dependency,
keep the existing supplemental text and update its provenance limitation to cite
this exact artifact/equivalent-code chain. Preserve the factual qualification
that the notice was added upstream after npm publication. If release policy
requires an original-distribution license file or an explicit publisher statement
covering `2.3.8`, that narrower requirement remains unmet by this evidence; the
investigation has not obtained such a statement.

Verification: tarball regular-file enumeration; registry SHA-512 verification;
all-file byte comparison between releases; source ancestry/changeset inspection;
SHA-256 equality with the checked-in supplement. No dependency tests were run
because this research changed no application or dependency files.
