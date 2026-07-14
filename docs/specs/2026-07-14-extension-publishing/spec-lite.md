# Spec Summary (Lite)

Prepare the extension for the Chrome Web Store and Edge Add-ons: a build/zip + version-bump pipeline
(optionally a GitHub Action), permission hardening that replaces `host_permissions: ["https://*/*"]`
with `optional_host_permissions` requested at runtime for the user's configured server origin, store
assets (icons, screenshots, descriptions), a published privacy policy (data goes only to the user's
own self-hosted server), and a documented submission runbook. Automated store submission is out of
scope — submission is manual using the runbook.
