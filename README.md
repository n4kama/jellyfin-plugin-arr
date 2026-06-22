# Jellyfin Arr Links

Adds a clickable **Sonarr** / **Radarr** link (with the app's logo) to the header of
every movie and TV show detail page in Jellyfin, right after the ★ rating — so it looks
native (like Jellyseerr/Overseerr).

- **Movies** → open the movie in **Radarr** (deep-linked by TMDb id).
- **Series / Seasons / Episodes** → open the **whole series** in **Sonarr**
  (resolved from the TVDb id via Sonarr's API, so an episode page still lands on the
  series).

## How it works (the short version)

Jellyfin plugins are server-side .NET assemblies and can't add UI directly, so the
plugin:

1. Patches the web client's `index.html` to load a tiny script (`ScriptInjectionService`).
   The web path is found at runtime via `IApplicationPaths.WebPath`, so the same build
   works on macOS / Linux / Windows / Docker.
2. That script adds the link, pointing at a plugin endpoint `Arr/Resolve/{itemId}`.
3. The endpoint looks the item up, maps it to Sonarr/Radarr, and `302`-redirects.
   Sonarr's API key stays server-side; the link carries your Jellyfin token as
   `?api_key=` with `rel="noopener noreferrer"` so it isn't leaked onward.

## Build

No local .NET SDK needed — everything runs through [`just`](https://just.systems) + Docker:

```sh
just build      # compile in Docker -> dist/ (DLL + meta.json)
just deploy     # build, then copy the DLL into the local Jellyfin plugins folder
just test       # run the inject.js self-check
just clean      # delete build artefacts (bin/ obj/ dist/)
just            # list recipes
```

## Install (macOS Jellyfin app)

```sh
just deploy
```

Then **quit and reopen** the Jellyfin app. (Other platforms: `just build`, then drop
`dist/*` into a `Arr Links_1.0.0.0` folder inside your Jellyfin `plugins` directory and
restart.)

## Configure

Dashboard → **Plugins** → **Arr Links** → fill in:

| Field            | Required? | Notes |
|------------------|-----------|-------|
| Sonarr URL       | yes       | Use the URL your **browser** reaches (external / reverse-proxy URL). |
| Sonarr API key   | yes       | Needed to resolve TVDb id → series. Sonarr → Settings → General. |
| Radarr URL       | yes       | As above. |

Refresh an open Jellyfin tab after the restart so it picks up the injected script.

## Caveats

- The link appears on every movie/series page even before you configure the URLs;
  clicking an unconfigured one just shows a "not configured" message.
- A **Jellyfin web update overwrites `index.html`** and removes the patch — it's
  re-applied automatically the next time the server starts.
- If the server can't write `index.html` (read-only web dir / permissions), the link
  is silently disabled and a warning is logged with the exact `<script>` tag to add
  manually before `</body>`.
- Radarr deep-links by TMDb id; almost every Jellyfin movie has one. A movie with no
  TMDb id won't get a link.

## Develop

`Web/inject.js` holds the client script; its pure helpers have a dependency-free
self-check:

```sh
just test
```
