# ADR 0013: Browser proxy attribution and account sessions

Date: 2026-10-30. Implementation decision for the approved frontend Phase 2.

.NET retains cookie authentication and existing IP-based abuse budgets. Next.js
connects from a server address, so using only that address would collapse all web
users into one CSRF/login/registration bucket. Fixed known Next.js egress addresses
are not assumed for the planned Vercel deployment.

Use a separate server-only 32-byte random shared secret (64 hexadecimal characters)
to authenticate Next-to-.NET client-address attribution. Next reconstructs
`X-RateFreak-Proxy-Key` and `X-RateFreak-Client-IP` from configuration and verified
platform input; it never forwards the caller's versions. .NET validates the key
in constant time and canonicalizes the address for rate-limit partitioning only.
It never changes user claims or synthesizes a session.

`BrowserProxy:SharedSecret` is optional for direct API deployments. Without it,
attribution headers have no effect. When configured, partial/invalid credentials
or address return sanitized no-store 403; header-free direct callers retain
socket-address budgets. IPv4-mapped IPv6 addresses normalize to the IPv4 bucket.

Production Next attribution requires Vercel runtime, HTTPS upstream and its
platform-provided `x-vercel-forwarded-for` value. Arbitrary configured header names,
trust-all forwarded-header middleware and public production loopback are excluded.
Development can explicitly select loopback mode against a loopback API; local
callers then share their normal local address budget. Other public hosting needs
a separately verified ingress adapter. Both secrets/settings must be configured;
this commit does not provision production infrastructure or rotate credentials.

The secret authenticates the service, so every holder can assert client addresses.
Keep it out of the browser, logs and source, transmit it only over HTTPS in
production, and restart both services when replacing it. It does not replace
perimeter abuse controls or coordinate rate limits between API instances.

Account writes additionally require the exact configured browser Origin, JSON,
CSRF and bounded bodies. UI session checks run on protected entry, mount,
focus/pageshow and every minute while visible. Credential-free BroadcastChannel
signals revalidate other windows; identity transitions cancel queries and clear
cache/CSRF state. The backend remains authoritative, including for expired or
revoked cookies between checks. No account data is embedded in static route HTML.

References:

- [Vercel request headers](https://vercel.com/docs/headers/request-headers)
- [ASP.NET proxy trust guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0)
