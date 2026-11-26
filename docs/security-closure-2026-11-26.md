# Security closure — allocator policy and OS C++ callers

**Policy update:** the owner subsequently authorized risk-based acceptance and
routine application of available compatible fixes. The two glibc findings below
are now accepted low risks under the [current policy](security-risk-policy.md).
The blocked disposition and receipts in this document describe the earlier
technical review; the [acceptance overlay](release-evidence/2026-11-26-security-risk-acceptance/decision.json)
supersedes its release-blocking decision without claiming a runtime fix.

**At the time of this technical review, release was blocked by two IDs:** CVE-2019-1010022 and
CVE-2019-1010024. Six of the previous eight IDs now have
conditional exclusions for the reviewed amd64 reference installation. This is
not package remediation, residual-risk acceptance or public-release clearance.

The [previous follow-up](security-followup-2026-11-25.md) remains historical
evidence. The [current dispositions](release-evidence/2026-11-26-security-closure/dispositions.json)
overlay its 22 open image/source records. All other assessments retain their
existing conditions. No scanner suppression or vendor-severity waiver was added.

## Applied allocator policy

The reference Compose package sets
`GLIBC_TUNABLES=glibc.malloc.arena_max=1` for PostgreSQL, API, web and storage
preparation; migration and maintenance inherit the API environment. The setting
is present before Python, Node, .NET or PostgreSQL starts and is inherited by
ordinary maintenance subprocesses. Changing it requires reassessment.

CVE-2019-1010025 concerns alignment of secondary malloc arenas created for
threads, rather than a disclosure of the `pthread_t` handle itself. The upstream
[original report](https://sourceware.org/pipermail/glibc-bugs/2018-February/041024.html)
identifies that mechanism. In the exact retained glibc 2.41 source,
`malloc/arena.c` initializes `narenas` to one and `arena_get2` refuses to create
another arena when `arena_max` is one. The selected Debian patch set does not
modify that logic. GNU documents the
[arena limit](https://sourceware.org/glibc/manual/latest/html_node/Memory-Allocation-Tunables.html).

Safe native probes created 16 concurrent allocating threads. Each of the four
candidate images produced 17 arenas by default and one with the policy.
The regression against the rendered Compose configuration first failed for all
six service roles, then passed for all six after the change. Review of the
application/native and relevant OS source sets found no supported caller that
raises the arena limit. This excludes the secondary-arena condition; it does
not claim to fix every allocator or address-randomization issue.

The policy may increase allocator contention. Fresh HTTPS functionality and a
small concurrent-read smoke passed; representative capacity qualification remains
a release gate. The persistent developer installation was not changed.

## C++ caller closure

The previous review covered application/native source but left OS consumers
unresolved. Fresh checks reverified 150 C++ payload paths, including usr-merge
aliases and packaged foreign-architecture assets, against the bound candidate
image inventories. Package ownership identifies APT 3.0.4, ICU 76.1-4,
LLVM 19.1.7-3, Z3 4.13.3-1, libzstd 1.5.7+dfsg-1 and PostgreSQL 17.12's JIT
provider. Unowned application payloads retain the earlier Node, Sharp/libvips,
.NET and Skia source bindings. These exclusions concern supported amd64 execution;
they do not qualify other target architectures.

The exact Debian upstream archives and packaging/patch sets for APT, ICU, LLVM,
Z3 and libzstd were acquired and checked against descriptor SHA256 values.
The review covers 70,988 source/build/patch files, with multiline searches and
manual classification of the matches. Twenty-six unique APT/ICU/pzstd binaries
match their debug companions by SHA256 and Build ID. Their header tables contain
no GNU pbds/parallel paths, and preserve positive APT bitset/bool-vector header
observations. Those tables supplement the source review; header absence alone
does not establish a caller exclusion.
Neither source acquisition nor debug-package downloads independently verify
package signatures. No bit-for-bit rebuild or complete compiler attestation is
claimed. See the
[source review](release-evidence/2026-11-26-security-closure/source-review.json)
and [binary bindings](release-evidence/2026-11-26-security-closure/consumer-binding.json).

| Finding | Supported-caller exclusion |
| --- | --- |
| CVE-2026-102010 | The affected GNU `pb_ds` binary heap is absent from the additional OS source/build/patch sets. The prior application/native exclusion is retained; ordinary STL priority queues do not instantiate this extension. |
| CVE-2026-114690 | No OS build enables or instantiates the affected GNU parallel search. LLVM's three lexical matches are Clang macro-name/string diagnostics, not a parallel-mode build or algorithm call. |
| CVE-2026-114691 | APT constructor counts are `uint32_t` package counts or lengths of existing vectors of larger objects. ICU's non-default constructor uses an existing `vector<int32_t>` length; its JSON bookkeeping uses default construction and checked growth. LLVM's three constructors use existing product/candidate-vector lengths. Z3's production `vector<bool>` uses its own array template. These distinctions exclude the near-`SIZE_MAX` constructor wrap on amd64; they are not a general input-size waiver. |
| CVE-2026-115455 | APT, ICU, Z3 and pzstd do not instantiate wide-stream bitset extraction. LLVM's reviewed bitsets perform numeric/bit operations, not extraction from wide streams. JIT is also disabled under the previously qualified configuration. |
| CVE-2026-125709 | APT's direct `ostreambuf_iterator` targets an in-memory string stream, while its file transfers use ordinary stream operations. ICU's JSON output adapter uses `ostream.put`/`write`; its raw stream buffer adapter is input-only. The additional Node candidates are excluded by the source-branch/sink review below. |

Installed LLVM, Z3 and the PostgreSQL JIT provider are not assumed absent. The
existing exact-image SQL/provider/mapping proof excludes JIT activation in the
supported fixed-query installation. Arbitrary SQL, added plugins/native code,
diagnostic flags and custom startup commands remain outside that reviewed scope.
The PostgreSQL `pzstd` utility was included in the source inventory even though
reference backup/restore does not invoke it.

### Node raw stream-output review

A broader search found additional direct writes in Node's bundled simdjson
compatibility formatter and V8 Maglev graph printer. They were investigated
separately rather than assuming all Node output uses ordinary guarded streams.

- Node 24.22.3's Linux build rules select C++20. The bundled simdjson defaults
  to `std::string_view` under C++17 or later, excluding the nonstandard
  compatibility formatter containing `write_padding`. Preprocessing the exact
  source under C++14 retains that function as a positive control; C++20 removes
  it. Independent source review found no selection override outside the two
  amalgamated copies. This is source/build-rule branch proof using a host
  preprocessor, not an independently reproduced Node build.
- Maglev's `std::cout` graph-printer callers require `print-maglev-graph`,
  `print-maglev-graphs` or `trace-maglev-regalloc`. All three default off in the
  exact candidate executable. The fixed `node server.js` startup does not enable
  them, and the packaged image has no `NODE_OPTIONS`. The application has no V8
  flag setter. The only setter in packaged JavaScript, in Next's memory startup
  helper, enables `detect-ineffective-gcs-near-heap-limit`; its literal argument
  and file hash were checked.
- Turbolev's graph output uses `CodeTracer::StreamScope`, which selects V8's
  `OFStream`/`StdoutStream`. Their custom `std::streambuf` writes with
  `fputc`/`fwrite`, not GCC `basic_filebuf::overflow`. The remaining apparent
  matches are Windows-only custom buffering, a commented LIEF example, Windows
  DLL symbol-name strings and a `decltype` expression.

These exclusions supplement the previous ordinary Node/Sharp filebuf review.
The [Node proof](release-evidence/2026-11-26-security-closure/node-stream-proof.json)
binds the positive control, actual diagnostic defaults, source/sink hashes and
packaged flag-setting call. Changed compiler/source inputs, startup options,
diagnostic flags or added native callers require reassessment.

## Two remaining blockers

| Finding | Evidence and required next step |
| --- | --- |
| CVE-2019-1010022 | Limiting malloc arenas does not change the native thread-stack/TCB stack-guard layout. No applied correction or complete exclusion exists. A corrected native runtime/build or a separately proved protection of that mechanism is still required. |
| CVE-2019-1010024 | Turning the stack cache off unmapped retired stacks, but all four images still reused the same address in all 31 sequential comparisons. Upstream also questioned whether removing caching changes kernel address reuse. The unsuccessful setting was not applied. A fix must address actual predictability, not merely prove cache eviction. |

The stack-cache result agrees with the concern in the
[upstream discussion](https://sourceware.org/legacy-ml/libc-alpha/2019-11/msg00051.html).
The remaining ID/image/package counts are derived in the
[current inventory](release-evidence/2026-11-26-security-closure/remaining-findings.json).
The API's Ubuntu libc also exhibited the native arena/cache behavior in the
probe, although the previous scanner ledger did not list these three glibc IDs
for that image. The reference policy covers it too; the unresolved mitigation
properties are not declared safe based on the vendor's scanner classification.

## Validation and artifact scope

- All six rendered service roles: allocator regression failed before the policy
  and passed afterward using real native allocation.
- Exact prior API/web/tools images plus the pinned PostgreSQL Trixie image:
  fresh HTTPS setup, secure sessions, CSRF/origin rejection, ordinary/staff
  authorization, logout, artwork and preserved library state passed.
- Forty public-item reads at four-way concurrency passed. This is a synthetic
  smoke, not representative load/capacity evidence.
- Coordinated synthetic database/artwork/key backup and isolated restore
  preserved all 33 table fingerprints. Current independent erasure/TMDB policy
  replay, startup guarding and repeated replay passed; the fixture journal was
  empty. Disposable containers and volumes were removed.
- 77 operator, 26 release and 20 compliance tests passed. The initial release-test
  run hit sandbox `spawnSync git EPERM`; its log was preserved and the authorized
  retry passed. Backend/frontend code and image bytes did not change.

The [verification receipts](release-evidence/2026-11-26-security-closure/verification.json)
bind the exact images, Compose hash and evidence files. This work reuses the
previously qualified application images and changes their installation
configuration. The frozen installation archive remains historical and must be
regenerated during final stable candidate qualification. No new image build,
fresh scanner-database qualification, stable release, push or publication is
claimed. The previous scan's package associations are retained as the assessment
baseline rather than relabelled as a new scan.

Raw public-source review materials, local debug companions and complete probe
logs are retained under `.local/security-closure-2026-11-26/`. Private fixture
credentials/backups are excluded from the public evidence. A separate read-only
review found no blocking issue with the conditional closures and confirmed that
the two remaining glibc IDs held the release gate under the policy then in effect.
The subsequent risk acceptance linked at the top supersedes that decision.
