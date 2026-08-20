# Deploying to a Synology NAS

BookmarkFeeder runs as five containers behind one reverse-proxy gateway:

```
        gateway (YARP) - the only port you publish
          ├─ /api/{**} → webapi (.NET) → postgres
          └─ /{**}     → web (static nginx)
        compose-dashboard (telemetry, optional)
```

Only **gateway** and the dashboard publish host ports. `webapi`, `web` and `postgres` are internal.

**TLS is required**, and it is not optional the way it used to be: signing in uses PKCE, which
needs the browser's Web Crypto API, and browsers expose that only on secure origins. Step 8 sets it
up with `tailscale serve`.

The compose file is **generated from the Aspire AppHost** and committed at
`docker/docker-compose.yaml`. Never edit it by hand: change
`BookmarkFeeder.AppHost/Program.cs`, run `./scripts/docker-compose-generate.sh`, commit.
`--check` fails if the two drift.

---

## One-time: set up Auth0

Sign-in is delegated to Auth0, so there are no passwords to store here. The free tier covers a
personal deployment comfortably. You need **two** objects: an API (which names the audience your
tokens are minted for) and a SPA application (the web app itself).

The browser extension needs neither - it authenticates with a key you mint in the web app.

```bash
auth0 login

# The API. --offline-access=true is what allows refresh tokens to be issued at all; without it
# `offline_access` is silently dropped and you get signed out roughly every hour.
auth0 apis create --name "BookmarkFeeder API" \
  --identifier "https://api.bookmarkfeeder" \
  --signing-alg RS256 --offline-access=true --json

# The SPA application. --auth-method None means a public client using PKCE, with no secret.
auth0 apps create --name "BookmarkFeeder Web" --type spa --auth-method None \
  --grants code,refresh-token \
  --callbacks   "http://localhost:5180,http://localhost:5173,https://<machine>.<tailnet>.ts.net" \
  --logout-urls "http://localhost:5180,http://localhost:5173,https://<machine>.<tailnet>.ts.net" \
  --origins     "http://localhost:5180,http://localhost:5173,https://<machine>.<tailnet>.ts.net" \
  --refresh-token '{"rotation_type":"rotating","expiration_type":"expiring","token_lifetime":2592000,"idle_token_lifetime":1209600}' \
  --json
```

Note the API identifier is permanent - changing it later invalidates every token and every client
config, so pick it once.

### The `--origins` flag is not the setting you need

`--origins` writes **Allowed Origins (CORS)** (`allowed_origins`). The setting that silent token
refresh actually depends on is **Allowed Web Origins** (`web_origins`), and the CLI has no flag for
it - it is left `null`, so the first login works and the session then dies at the first token
refresh with a CORS error. Set it explicitly:

```bash
auth0 api patch "clients/<web-client-id>" \
  --data '{"web_origins":["http://localhost:5180","http://localhost:5173","https://<machine>.<tailnet>.ts.net"]}'
```

Verify with `auth0 api get "clients/<web-client-id>"` that `web_origins` is populated, not null.

### Check the connection, do not blindly patch it

New applications are usually enabled on the tenant's connections automatically, so there is often
nothing to do. Check first:

```bash
auth0 api get "connections"                      # find Username-Password-Authentication and its id
auth0 api get "connections/<id>/clients"         # the authoritative list
```

If your new client id is listed, you are done.

⚠️ **Do not reach for `--data '{"enabled_clients":[...]}'`.** That field is deprecated: it reads
back empty even when the connection has clients, so "fix" it and you would blank the real list -
breaking sign-in for every *other* application in the tenant. The live data is behind
`/connections/<id>/clients`, and connections are managed through the dashboard or the dedicated
endpoints.

Keep the client id from `auth0 apps create`; it goes into `.env` in step 5.

> **One trap worth knowing.** Auth0 identifies people by the `sub` claim, and the same person
> signing in through a database account (`auth0|...`) and through Google (`google-oauth2|...`) gets
> two different `sub`s - and therefore two separate, invisible collections. Keep only the database
> connection enabled to start with; if you add a social connection later, configure Account Linking
> first.

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
| `AUTH0_DOMAIN` | Your tenant, e.g. `usualexpat.uk.auth0.com`. Not a secret. |
| `AUTH0_AUDIENCE` | The API identifier from the Auth0 step, e.g. `https://api.bookmarkfeeder`. Not a secret. |
| `AUTH0_CLIENT_ID` | The SPA application's client id. Public by design - it ships in the browser bundle. |
| `POSTGRES_PASSWORD` | `openssl rand -base64 24`. **Pick once** - the database keeps the password from first boot; changing it later locks the API out of its own data. |
| `GATEWAY_PORT` | The port you'll browse to. Defaults to 8081 - 8080 is a busy port (Glance uses it here) and DSM itself takes 5000/5001. |
| `*_IMAGE` | The three version tags from step 1. |

You only ever revisit the `*_IMAGE` lines. `aspire publish` never overwrites this file - it only
adds missing keys - so regenerating the compose leaves your secrets alone.

The three `AUTH0_*` values feed **both** the `webapi` and the `web` container: the API validates
tokens with them, and the web container's nginx entrypoint writes them into `config.js` at start.
That is why the published web image works for any tenant without being rebuilt.

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

At this point the stack is running, but **you cannot sign in yet** - see the next step.

### 8. Put TLS in front - required

```bash
# Once, in the Tailscale admin console: DNS -> HTTPS Certificates -> Enable
tailscale serve --bg --https=443 http://127.0.0.1:8081
tailscale serve status        # prints https://<machine>.<tailnet>.ts.net/
```

Why this is not optional. `auth0-spa-js` derives its PKCE code challenge with `crypto.subtle`, and
browsers expose the Web Crypto API only on **secure origins**. `https://<machine>.<tailnet>.ts.net`
qualifies - Tailscale issues a real Let's Encrypt certificate. `http://<nas>:8081` and
`http://192.168.1.x:8081` do not, and the app fails at startup with an opaque error about reading
`digest` of undefined.

`localhost` *is* a secure origin by specification, which is why local development needs none of
this.

This works even though your tailnet is private, which is counter-intuitive enough to be worth
stating: Authorization Code + PKCE is entirely front-channel. Auth0 redirects your *browser*, and
your browser is on the tailnet. Auth0 never needs to reach your NAS.

Use `tailscale serve`, **not `tailscale funnel`**. Funnel publishes to the open internet, and
signup is open by default.

Finally, make sure that `ts.net` URL is listed in your Auth0 application's **Allowed Callback
URLs**, **Allowed Logout URLs** and **Allowed Web Origins**.

### 9. Sign in

Open `https://<machine>.<tailnet>.ts.net` and create an account. The first account to sign in
**inherits any bookmarks that were already there**, so sign in yourself before sharing the URL.

Then, for each browser you want to sync from:

1. In the web app, go to **Settings → API keys** and create a key named after that browser
2. Copy it - it is shown once
3. Open the extension popup, set the server URL to
   `https://<machine>.<tailnet>.ts.net/api`, paste the key, and hit **Test connection**

Each browser gets its own key so you can revoke one without disturbing the others. A key can upload
bookmarks and read tags and categories; it cannot delete anything or mint more keys.

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

**A blank page, and the console says `Cannot read properties of undefined (reading 'digest')`.**
You are on a plain `http://` address. Sign-in needs `crypto.subtle`, which browsers expose only on
secure origins. Use the `ts.net` URL from step 8. The app now detects this and shows an explanatory
screen instead of a blank page, but an older build will not.

**"Callback URL mismatch" from Auth0.** The exact origin you browsed to is not in the application's
Allowed Callback URLs. It must match scheme, host and port exactly - `https://nas.tailnet.ts.net`
and `https://nas.tailnet.ts.net:443` are the same, but `http://` and `https://` are not.

**Signed out roughly every hour.** Two usual causes: **Allow Offline Access** is off on the API (so
no refresh token is ever issued), or the origin is missing from **Allowed Web Origins** (so the
silent refresh call is blocked by CORS). The first login works either way, which is what makes this
confusing - the failure only shows up when the access token first expires.

**"Unauthorized" on the very first login attempt.** The application is not enabled on the database
connection. See the end of the Auth0 section.

**"I signed in with Google and my bookmarks are gone."** Auth0 gives a database identity and a
Google identity different `sub` values, and collections are scoped to `sub` - so that is a second,
empty account, not lost data. Sign back in the original way. Enable Account Linking if you want
both to work.

**Every request 401s and the logs mention `IDX10500` or a JWKS failure.** The API cannot reach
`https://<your-domain>/.well-known/jwks.json` to fetch Auth0's signing keys. Check outbound HTTPS
from the container. Note the browser extension is unaffected by this - key validation is entirely
local.

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

**I lost an API key.** They cannot be recovered - only a hash is stored. Revoke it in
**Settings → API keys** and create a replacement.

**The dashboard on `:18888`** is unauthenticated and shows request telemetry. Keep it on the LAN;
never port-forward it. Remove the service from the compose if you would rather not run it.
