repo: antonkesy/YAWSS
branch: main
path: web/src

## Last sync
date: 2026-09-28T11:27:09Z

### Updated in this project
- Redesigned the home, review and export flow (Scanner Stage v2)
- Past scans save automatically and show a status (draft / exported / downloaded / discarded)
- Save / Export writes to an export folder you set in settings

## Screen map
| Screen | Repo files |
| --- | --- |
| Scanner Stage v2 · Home | web/src/routes/+page.svelte, web/src/lib/components/StatusPill.svelte, ScanButton.svelte, SessionList.svelte |
| Scanner Stage v2 · Review | web/src/routes/session/[id]/+page.svelte, web/src/lib/components/PageGrid.svelte, Toolbar.svelte, Preview.svelte, ExportBar.svelte |
| Scanner Stage v2 · Settings sheet | web/src/routes/settings/+page.svelte, web/src/lib/settings.ts |
| Scanner Prototype (A/B/C) | same as above |
