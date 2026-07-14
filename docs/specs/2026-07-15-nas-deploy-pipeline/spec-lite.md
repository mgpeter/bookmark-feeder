# Spec Summary (Lite)

Get BookmarkFeeder running on the Synology NAS via a repeatable Aspire-driven flow: `aspire publish`
emits the compose artifact, the webapi/gateway/web images are built for linux-amd64 and pushed to
Docker Hub, and the NAS pulls and runs them behind the single gateway port. Pins the production
Postgres and dashboard image versions, gives the API key and DB password stable values so the NAS
`.env` is filled once and survives republishing, and replaces the deployment doc, which currently
describes a flow that cannot work.
