# Editorial app pages and dark theme

User request: apply the approved homepage design to the existing pages and add
a dark app theme. Continue the approved code-first native workflow on the current
branch, preserving the uncommitted homepage work. Catalog remains a later slice.

## Design and scope

Promote the approved ivory/ink/oxblood palette and typography to shared tokens.
Login, registration and account inherit the wordmark, form treatments, serif
headings and calm spacing. Keep forms focused and account information readable;
remove the old decorative eyebrow. Preserve all authentication/session behavior.

Add a warm ink dark palette with cream foreground and rose accent. A labeled
native Theme select in the header offers Light, Dark and System. Builder default:
Light on first visit; store only the theme preference in browser localStorage.
Apply saved preference before hydration, follow OS changes only in System mode,
sync other tabs, and keep switching functional if browser storage is blocked.
No new dependencies, backend changes or generated images.

## Steps

- [x] Add failing behavior tests for theme initialization, persistence, System
  changes, storage events, invalid values and blocked storage.
- [x] Add a small shared theme store and pre-paint initialization script; expose
  the preference through a native labeled header select.
- [x] Promote shared tokens and shell styles; remove homepage-only theme selection.
- [x] Restyle shared form/button/feedback primitives, login/register/account pages,
  and error states for both themes. Preserve labels, guidance and session logic.
- [x] Run all frontend tests, lint, typecheck and production build.
- [x] Inspect all existing pages in both themes, at mobile/desktop widths;
  verify persistence, keyboard use, first paint and navigation without input loss.
- [x] Request independent finish review; record evidence and update DESIGN.md
  and frontend checklist. No automatic commits, pushes or deployment.
