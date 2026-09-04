# FolderFolio

FolderFolio is a small, self-hosted photography portfolio. Its filesystem is the content-management system: create, rename, replace, or remove photo files and the running application indexes the change.

## Run with Docker Compose

Prerequisites are Docker Engine with the Compose plugin and a host directory containing your photos. Create an album directory, for example:

```sh
mkdir -p photos/01-Landscapes
docker compose up -d --build
```

Put images directly in album directories. The Compose origin listens only on `127.0.0.1:8080`; a host-based Cloudflare Tunnel should use `http://localhost:8080` as its origin. Do not expose this origin on a non-loopback interface unless you configure explicit trusted proxies first: FolderFolio accepts one trusted forwarded hop for the documented local-tunnel arrangement.

```sh
curl -i http://127.0.0.1:8080/health
docker compose logs --no-color folderfolio
```

`GET /health` returns JSON with the index status, generation, album total, photo total, and last successful scan time. It returns `200` only when the index is `ready`; it returns `503` during initial indexing and when the index is degraded. Visitors may still receive the last valid portfolio while health is degraded.

## Photo folders

Each immediate directory under `PhotoRoot` is one album. FolderFolio considers only files immediately inside each album directory; nested directories are not albums and their images are ignored. Supported extensions are `.jpg`, `.jpeg`, `.png`, and `.webp`, case-insensitively. Symbolic links and other reparse points, corrupt images, and unreadable files are skipped.

An album directory can begin with digits followed by `-` or `_` to establish its order, such as `01-Landscapes`. The recognized prefix is removed from the display title. Remaining dashes and underscores become spaces, repeated whitespace is collapsed, and the title is trimmed. Albums with numeric prefixes sort first by number; the rest sort alphabetically. URLs use a lowercase, URL-safe ASCII slug of that title; collisions receive a stable hash suffix.

Photos with EXIF `DateTimeOriginal` sort first by that value, followed by `DateTimeDigitized`; no timezone conversion is applied. The filename breaks ties. Undated photos follow in case-insensitive filename order.

## Configuration

Settings live in the `FolderFolio` configuration section and can be overridden with ASP.NET Core environment variables using `FolderFolio__...` names. Changes require an application restart.

| Variable | Default | Meaning |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` in Compose | Selects the production middleware behavior, including production exception handling and HSTS. |
| `ASPNETCORE_HTTP_PORTS` | `8080` in the container image | Sets the container listener port; Compose maps host loopback port `8080` to it. |
| `FolderFolio__PhotoRoot` | `/photos` | Required absolute root containing immediate album directories. |
| `FolderFolio__CacheRoot` | `/cache` | Required absolute derivative-cache path. |
| `FolderFolio__GridLongEdge` | `400` | Positive maximum long edge for gallery derivatives. |
| `FolderFolio__WebLongEdge` | `2000` | Positive maximum long edge for lightbox derivatives; at least the grid edge. |
| `FolderFolio__WebPQuality` | `82` | WebP quality, from 1 through 100. |
| `FolderFolio__SiteTitle` | `FolderFolio` | Required nonblank home-page title. |
| `FolderFolio__Tagline` | `Photos from a folder.` | Optional home-page tagline. |

Compose mounts `./photos` at `/photos` read-only and keeps generated WebP derivatives in the persistent named volume `folderfolio-cache` at `/cache`. The image runs as the non-root `app` user (UID 1654). If replacing the named volume with a Linux bind mount for the cache, make that directory writable by UID 1654; do not resolve permissions by running the service as root.

Derivative URLs are versioned from their source and image-output settings. Replacing a source at the same filename therefore produces a new URL after the watcher quiet period. Old versioned derivatives are intentionally retained. To reclaim space, stop the service, clear the cache, and start it again; missing derivatives regenerate on demand.

## Local development

Install the .NET SDK selected by `global.json`, make suitable local photo and cache directories, then supply absolute paths when running the app:

```sh
dotnet run --project src/FolderFolio -- --FolderFolio:PhotoRoot=/absolute/path/to/photos --FolderFolio:CacheRoot=/absolute/path/to/cache
```

## MVP boundaries

This deliberately small MVP has no database, administration interface, upload flow, login or authentication, search, tags, captions, frontend EXIF display, nested albums, focal-point editing, cache eviction, periodic full reconciliation, slideshows, swipe gestures, or browser-automation tests. It is a single Linux container instance rather than a multi-instance cluster.

Media derivatives are generated as WebP after orientation is applied and EXIF/IPTC/XMP metadata is removed, so public files do not expose camera GPS or other embedded metadata. FolderFolio uses SixLabors ImageSharp; review its split-license terms for your intended use. This is an operational reminder, not legal advice.
