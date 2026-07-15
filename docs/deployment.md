# Deploying to a Synology NAS

BookmarkFeeder runs as five containers behind one reverse-proxy gateway:

```
        gateway (YARP) - the only port you publish
          ├─ /api/{**} → webapi (.NET) → postgres
          └─ /{**}     → web (static nginx)
        compose-dashboard (telemetry, optional)
```

Only **gateway** and the dashboard publish host ports. `webapi`, `web` and `postgres` are internal.
TLS is not included - put DSM's own reverse proxy in front if you want HTTPS.

The compose file is **generated from the Aspire AppHost** and committed at
`docker/docker-compose.yaml`. Never edit it by hand: change
`BookmarkFeeder.AppHost/Program.cs`, run `./scripts/docker-compose-generate.sh`, commit.
`--check` fails if the two drift.

---

## One-time: on your machine

### 1. Release the images

```powershell
docker login -u mgpeter
.\scripts\docker-release.ps1 -Minor      # or -Patch / -Major
```

Bumps `VERSION`, builds all three images for **linux/amd64**, pushes `:<version>` and `:latest`,
and prints the three `.env` lines to paste. `VERSION` is only written once the push succeeds.

Deploy the **version tag, never `:latest`** - otherwise "which build is running?" has no answer and
a restart can silently change the app.

### 2. Try it locally first (recommended)

```bash
cp docker/.env.local.template docker/.env
docker compose -f docker/docker-compose.yaml up -d
# http://localhost:8081
```

Same images, same compose, same wiring as the NAS. Two bugs that made every container fail
(migrations skipped; nginx on the wrong port) were invisible in the YAML and obvious here. If it
works locally it will work on the NAS.

---

## On the NAS

### 3. Create the folder

Over SSH (Control Panel → Terminal & SNMP → Enable SSH), or via File Station:

```bash
mkdir -p /volume1/docker/bookmarkfeeder
```

Everything lives here: the compose file, `.env`, and - importantly - the database. Docker commands
on DSM need `sudo` unless your user is in the `docker` group.

### 4. Copy two files

From your machine (File Station, or scp):

```bash
scp docker/docker-compose.yaml  you@nas:/volume1/docker/bookmarkfeeder/
scp docker/.env.nas.template    you@nas:/volume1/docker/bookmarkfeeder/.env
```

Only these two. The NAS needs no source, no .NET SDK and no Aspire.

### 5. Fill in `.env` - once

```bash
cd /volume1/docker/bookmarkfeeder
vi .env          # or edit it in File Station
chmod 600 .env   # it holds real secrets
```

Set:

| Key | What |
|---|---|
| `API_KEY` | The **only** thing protecting your collection. `openssl rand -base64 32`. Never the dev key. |
| `POSTGRES_PASSWORD` | `openssl rand -base64 24`. **Pick once** - the database keeps the password from first boot; changing it later locks the API out of its own data. |
| `GATEWAY_PORT` | The port you'll browse to. Defaults to 8081 - 8080 is a busy port (Glance uses it here) and DSM itself takes 5000/5001. |
| `*_IMAGE` | The three version tags from step 1. |

You only ever revisit the `*_IMAGE` lines. `aspire publish` never overwrites this file - it only
adds missing keys - so regenerating the compose leaves your secrets alone.

### 6. Start it

```bash
cd /volume1/docker/bookmarkfeeder
sudo docker compose up -d
```

First start pulls the images, creates `data/postgres`, and **applies the EF migrations
automatically** - no manual schema step.

Alternatively use **Container Manager → Project → Create**, point it at
`/volume1/docker/bookmarkfeeder`, and it will pick up `docker-compose.yaml`. That gives you
start/stop/logs in DSM. (The CLI path above is the one tested end to end.)

### 7. Check it

```bash
sudo docker compose ps                              # all up; postgres (healthy)
sudo docker compose logs webapi | grep -i migrat    # migrations applied
```

Then open `http://<nas>:8081`. You land on **Settings** - paste the `API_KEY` from `.env`. It is
stored in your browser, not on the server.

Point the browser extension at `http://<nas>:8081/api` with the same key.

---

## Where your data lives

```
/volume1/docker/bookmarkfeeder/data/postgres
```

A **bind mount**, not a Docker named volume. A named volume would survive restarts perfectly well,
but it lives in `/volume1/@docker/volumes` - invisible in File Station and not a shared folder
Hyper Backup can select. This way the database sits in a normal folder you can see, snapshot and
back up.

**This directory is the only thing worth backing up.** Everything else rebuilds from the images.
Point Hyper Backup at `/volume1/docker/bookmarkfeeder` and you have the lot.

It survives `docker compose down`, container removal and reboots. It does **not** survive
`docker compose down -v` or deleting the folder.

Set `POSTGRES_DATA_PATH` in `.env` to put it on a different volume.

---

## Updating

```powershell
.\scripts\docker-release.ps1 -Patch       # on your machine
```

Then on the NAS, change the three `*_IMAGE` tags in `.env` and:

```bash
sudo docker compose pull && sudo docker compose up -d
```

Migrations for any new schema apply on start. Your data and secrets are untouched.

If the AppHost changed (ports, services, healthchecks), re-copy `docker-compose.yaml` too -
`./scripts/docker-compose-generate.sh` regenerates it.

---

## Troubleshooting

**A certificate error on `http://<host>:8081`.** Nothing here redirects to HTTPS. It is your
browser: HSTS is host-wide and **ignores ports**, so if anything on that hostname ever sent an HSTS
header, every port on it gets force-upgraded. Clear it at `chrome://net-internals/#hsts` →
*Delete domain security policies*. An incognito window confirms the diagnosis quickly.

**`port is already allocated`.** Project names scope networks and containers, but published ports
are host-global. Change `GATEWAY_PORT`; the gateway's internal port follows, because both sides read
the same variable.

**Everything 502s.** The gateway cannot reach `web` or `webapi`. `sudo docker compose ps` - check
they are up, and that `web` is listening on 8000 (nginx's default 80 is wrong here).

**`3D000: database "bookmarkfeeder" does not exist`.** Migrations did not run. Fixed in 0.1.1;
`0.1.0` is broken and must not be deployed.

**The dashboard on `:18888`** is unauthenticated and shows request telemetry. Keep it on the LAN;
never port-forward it. Remove the service from the compose if you would rather not run it.
