# Spec Requirements Document

> Spec: Extension Packaging & Store Publishing
> Created: 2026-07-14
> Status: Planning

## Overview

Prepare the extension for distribution on the Chrome Web Store and Edge Add-ons: a build/zip +
version-bump pipeline, narrowed host permissions (request the user-configured server origin at
runtime instead of `https://*/*`), and the store assets, privacy policy, and submission steps a
data-minimal, self-hosted extension needs.

## User Stories

### Install it like a normal extension

As a user, I want to install BookmarkFeeder from the Chrome Web Store / Edge Add-ons, so that I don't
have to sideload an unpacked folder and can get updates automatically.

The extension is packaged, versioned, and submitted with the required assets and a privacy policy;
installation only asks for the permissions it actually needs.

### Trust what it can access

As a privacy-conscious user, I want the extension to request access only to my own BookmarkFeeder
server, so that it isn't granted access to every website.

Broad host access is removed; the extension requests permission for the specific configured server
origin at runtime when the user saves their settings.

## Spec Scope

1. **Build & release pipeline** - npm scripts (optionally a GitHub Action) that build and `.zip` the
   extension and bump the manifest version for a release.
2. **Permission hardening** - Replace `host_permissions: ["https://*/*"]` with
   `optional_host_permissions`, requesting the configured server origin at runtime via
   `chrome.permissions.request`.
3. **Store assets** - Icons, screenshots, promo images, and store descriptions.
4. **Privacy policy** - A published privacy policy reflecting that data goes only to the user's own
   self-hosted server (no third-party collection).
5. **Submission runbook** - Documented Chrome Web Store + Edge Add-ons submission steps.

## Out of Scope

- The popup redesign/build and background sync (previous specs).
- Automated store submission/CD (manual submission using the runbook; a build/zip CI job is optional).
- Paid/enterprise distribution channels.

## Expected Deliverable

1. `npm run build` (or a release script) produces a versioned, store-uploadable `.zip`.
2. A freshly installed build requests access only to the user's configured server origin (granted at
   settings-save time), not all sites.
3. Store assets, a published privacy policy, and a submission runbook exist so the extension can be
   submitted to the Chrome Web Store and Edge Add-ons.
