# scripts

Build and release the BookmarkFeeder container images. `.ps1` and `.sh` are equivalent — use
whichever shell you're in.

| Script | Does |
|---|---|
| `docker-compose-generate` | Regenerates `docker/docker-compose.yaml` from the Aspire AppHost. |
| `docker-build` | Builds `webapi`, `gateway`, `web` at the current `VERSION`. No bump, no push. |
| `docker-release` | Bumps `VERSION`, builds, pushes `:<version>` and `:latest` to Docker Hub. |

Version lives in **`VERSION`** at the repo root (semver). `docker-release` bumps it, so `VERSION`
is the *last released* version — it is written **only after the push succeeds**, so a failed
release leaves it untouched and re-running retries the same number rather than skipping one.
`--no-push` builds without releasing and therefore does not bump it.

```bash
./scripts/docker-compose-generate.sh          # AppHost -> docker/docker-compose.yaml
./scripts/docker-compose-generate.sh --check  # fail if the committed compose is stale
./scripts/docker-build.sh                     # build the current version locally
./scripts/docker-release.sh --dry-run         # show the bump, change nothing
./scripts/docker-release.sh --minor           # 0.1.0 -> 0.2.0, build, push
./scripts/docker-release.sh --patch --no-push
```

```powershell
./scripts/docker-compose-generate.ps1
./scripts/docker-compose-generate.ps1 -Check
./scripts/docker-build.ps1
./scripts/docker-release.ps1 -DryRun
./scripts/docker-release.ps1 -Minor
./scripts/docker-release.ps1 -Patch -NoPush
```

## The compose file is generated

`docker/docker-compose.yaml` comes from `BookmarkFeeder.AppHost/Program.cs` — **never edit it by
hand.** Change the AppHost, run `docker-compose-generate`, commit the result. `--check` fails if the
two have drifted, which is what stops the file quietly becoming a second source of truth.

It is committed because it holds no literal secrets (every one is a `${VAR}`), so releases are
diffable and the NAS needs no Aspire install. One compose serves both local and NAS — they differ
only in `.env` values, so there is nothing to keep in sync.

```bash
cp docker/.env.local.template docker/.env   # first time only
docker compose -f docker/docker-compose.yaml up -d   # http://localhost:8081
```

`docker/.env` is gitignored; `.env.local.template` and `.env.nas.template` are committed with
placeholders. Regenerating never clobbers a filled-in `.env` — `aspire publish` only adds missing
keys (`onlyIfMissing`).

Images: `mgpeter/bookmarkfeeder-{webapi,gateway,web}`, all **linux/amd64** (the NAS). `webapi` and
`gateway` build via the .NET SDK's container support — no Dockerfile; `web` uses its own
(Vite build → static nginx).

## Notes

- **`docker login -u mgpeter` first.** The scripts never handle credentials, and deliberately do
  not pre-check the login: Docker Desktop keeps credentials in the OS credential manager
  (`credsStore`), so `docker info` reports no `Username` and `config.json`'s `auths` entries are
  empty — every cheap check gives false negatives and blocks real releases. `docker push` reports
  `denied: requested access to the resource is denied` clearly enough, and since `VERSION` is only
  written after a successful push, a missing login costs a rebuild rather than a wrong version.
- **Stop the Aspire AppHost before building** — it holds `BookmarkFeeder.WebApi.exe` open and the
  build fails on a file lock. The scripts check and say so.
- **Deploy the NAS by the version tag, never `:latest`**, so "which build is running?" always has
  an answer. `docker-release` prints the `.env` lines to paste.
- The images carry no secrets: the API key reaches `webapi` as runtime env, and the `web` bundle
  bakes nothing (Vite ignores `.env.development` in a production build). Verified by inspection —
  worth re-checking if that ever changes, since the repos are public.
- `-t:PublishContainer`, never `/t:` — Git Bash rewrites `/t:...` into a Windows path and MSBuild
  then reports the unhelpful *"MSB1008: Only one project can be specified"*.

See `docs/deployment.md` for the full NAS deploy.
