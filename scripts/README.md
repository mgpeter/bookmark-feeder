# scripts

Build and release the BookmarkFeeder container images. `.ps1` and `.sh` are equivalent — use
whichever shell you're in.

| Script | Does |
|---|---|
| `docker-build` | Builds `webapi`, `gateway`, `web` at the current `VERSION`. No bump, no push. |
| `docker-release` | Bumps `VERSION`, builds, pushes `:<version>` and `:latest` to Docker Hub. |

Version lives in **`VERSION`** at the repo root (semver). `docker-release` bumps it, so `VERSION`
is the *last released* version.

```bash
./scripts/docker-build.sh                   # build the current version locally
./scripts/docker-release.sh --dry-run       # show the bump, change nothing
./scripts/docker-release.sh --minor         # 0.0.0 -> 0.1.0, build, push
./scripts/docker-release.sh --patch --no-push
```

```powershell
./scripts/docker-build.ps1
./scripts/docker-release.ps1 -DryRun
./scripts/docker-release.ps1 -Minor
./scripts/docker-release.ps1 -Patch -NoPush
```

Images: `mgpeter/bookmarkfeeder-{webapi,gateway,web}`, all **linux/amd64** (the NAS). `webapi` and
`gateway` build via the .NET SDK's container support — no Dockerfile; `web` uses its own
(Vite build → static nginx).

## Notes

- **`docker login -u mgpeter` first.** The scripts check before building rather than after, and
  never handle credentials themselves.
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
