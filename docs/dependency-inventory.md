# Dependency inventory snapshot

Reviewed: 2026-11-13, source revision `b9d3ca3`.

This is local package metadata, not a final image SBOM or a complete notice bundle.
See [scope and release actions](dependency-redistribution-review.md).

## Installed web production dependency closure

Started from `web/package.json` dependencies, followed installed dependencies and
available optionalDependencies through Node-style parent node_modules resolution,
and deduplicated real package paths. Missing optional platform binaries are excluded.
The 63 entries include build dependencies reachable from Next.js, not just files
proven to be in the standalone output. Peer dependencies are represented only where
reachable through this traversal; final bundle/image inspection remains required.

| Package | Version | Declared license |
| --- | --- | --- |
| @floating-ui/core | 1.8.0 | MIT |
| @floating-ui/dom | 1.8.0 | MIT |
| @floating-ui/react-dom | 2.1.9 | MIT |
| @floating-ui/utils | 0.2.12 | MIT |
| @img/colour | 1.1.0 | MIT |
| @img/sharp-libvips-linux-x64 | 1.3.4 | LGPL-3.0-or-later |
| @img/sharp-linux-x64 | 0.35.5 | Apache-2.0 |
| @next/env | 16.4.3 | MIT |
| @next/swc-linux-x64-gnu | 16.4.3 | MIT |
| @radix-ui/number | 1.1.3 | MIT |
| @radix-ui/primitive | 1.2.0 | MIT |
| @radix-ui/react-arrow | 1.1.17 | MIT |
| @radix-ui/react-collection | 1.2.0 | MIT |
| @radix-ui/react-compose-refs | 1.1.5 | MIT |
| @radix-ui/react-context | 1.3.0 | MIT |
| @radix-ui/react-direction | 1.2.0 | MIT |
| @radix-ui/react-dismissable-layer | 1.2.1 | MIT |
| @radix-ui/react-focus-guards | 1.2.0 | MIT |
| @radix-ui/react-focus-scope | 1.3.1 | MIT |
| @radix-ui/react-id | 1.1.4 | MIT |
| @radix-ui/react-popper | 1.4.0 | MIT |
| @radix-ui/react-portal | 1.2.0 | MIT |
| @radix-ui/react-presence | 1.1.11 | MIT |
| @radix-ui/react-primitive | 2.2.0 | MIT |
| @radix-ui/react-select | 2.4.1 | MIT |
| @radix-ui/react-slot | 1.5.0 | MIT |
| @radix-ui/react-use-callback-ref | 1.1.5 | MIT |
| @radix-ui/react-use-controllable-state | 1.3.0 | MIT |
| @radix-ui/react-use-effect-event | 0.0.5 | MIT |
| @radix-ui/react-use-layout-effect | 1.1.4 | MIT |
| @radix-ui/react-use-rect | 1.1.4 | MIT |
| @radix-ui/react-use-size | 1.1.5 | MIT |
| @radix-ui/react-visually-hidden | 1.2.13 | MIT |
| @radix-ui/rect | 1.1.3 | MIT |
| @swc/helpers | 0.5.23 | Apache-2.0 |
| @tabler/icons | 3.50.0 | MIT |
| @tabler/icons-react | 3.50.0 | MIT |
| @tanstack/query-core | 5.105.0 | MIT |
| @tanstack/react-query | 5.105.0 | MIT |
| aria-hidden | 1.2.6 | MIT |
| baseline-browser-mapping | 2.11.34 | Apache-2.0 |
| caniuse-lite | 1.0.30001820 | CC-BY-4.0 |
| client-only | 0.0.1 | MIT |
| detect-libc | 2.1.2 | Apache-2.0 |
| detect-node-es | 1.1.0 | MIT |
| get-nonce | 1.0.1 | MIT |
| nanoid | 3.3.20 | MIT |
| next | 16.4.3 | MIT |
| picocolors | 1.1.1 | ISC |
| postcss | 8.5.23 | MIT |
| react | 19.3.0 | MIT |
| react-dom | 19.3.0 | MIT |
| react-remove-scroll | 2.7.2 | MIT |
| react-remove-scroll-bar | 2.3.8 | MIT |
| react-style-singleton | 2.2.3 | MIT |
| scheduler | 0.28.0 | MIT |
| semver | 7.8.5 | ISC |
| sharp | 0.35.5 | Apache-2.0 |
| source-map-js | 1.2.2 | BSD-3-Clause |
| styled-jsx | 5.1.6 | MIT |
| tslib | 2.8.1 | 0BSD |
| use-callback-ref | 1.3.3 | MIT |
| use-sidecar | 1.1.3 | MIT |

## Restored API package graph

From `src/RateFreak.Api/obj/project.assets.json`, with license expressions from
the corresponding locally cached NuGet `.nuspec` files. Includes design/build
packages and non-Linux native assets; this is not a Linux publish manifest. Test
projects and the separately restored EF CLI tool graph are outside this inventory.

| Package | Version | Declared license |
| --- | --- | --- |
| Humanizer.Core | 2.14.1 | MIT |
| Microsoft.AspNetCore.Authentication.Google | 10.0.2 | MIT |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.2 | MIT |
| Microsoft.AspNetCore.OpenApi | 10.0.2 | MIT |
| Microsoft.Build.Framework | 18.0.2 | MIT |
| Microsoft.CodeAnalysis.Analyzers | 3.11.0 | MIT |
| Microsoft.CodeAnalysis.CSharp.Workspaces | 5.0.0 | MIT |
| Microsoft.CodeAnalysis.CSharp | 5.0.0 | MIT |
| Microsoft.CodeAnalysis.Common | 5.0.0 | MIT |
| Microsoft.CodeAnalysis.Workspaces.Common | 5.0.0 | MIT |
| Microsoft.CodeAnalysis.Workspaces.MSBuild | 5.0.0 | MIT |
| Microsoft.EntityFrameworkCore.Abstractions | 10.0.2 | MIT |
| Microsoft.EntityFrameworkCore.Analyzers | 10.0.2 | MIT |
| Microsoft.EntityFrameworkCore.Design | 10.0.2 | MIT |
| Microsoft.EntityFrameworkCore.Relational | 10.0.2 | MIT |
| Microsoft.EntityFrameworkCore | 10.0.2 | MIT |
| Microsoft.Extensions.DependencyModel | 10.0.2 | MIT |
| Microsoft.OpenApi | 2.7.5 | MIT |
| Microsoft.VisualStudio.SolutionPersistence | 1.0.52 | MIT |
| Mono.TextTemplating | 3.0.0 | MIT |
| Newtonsoft.Json | 13.0.3 | MIT |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.0 | PostgreSQL |
| Npgsql | 10.0.0 | PostgreSQL |
| SkiaSharp.NativeAssets.Linux.NoDependencies | 4.155.0 | MIT |
| SkiaSharp.NativeAssets.Win32 | 4.155.0 | MIT |
| SkiaSharp.NativeAssets.macOS | 4.155.0 | MIT |
| SkiaSharp | 4.155.0 | MIT |
| Swashbuckle.AspNetCore.SwaggerUI | 10.3.1 | MIT |
| System.CodeDom | 6.0.0 | MIT |
| System.Composition.AttributedModel | 9.0.0 | MIT |
| System.Composition.Convention | 9.0.0 | MIT |
| System.Composition.Hosting | 9.0.0 | MIT |
| System.Composition.Runtime | 9.0.0 | MIT |
| System.Composition.TypedParts | 9.0.0 | MIT |
| System.Composition | 9.0.0 | MIT |
