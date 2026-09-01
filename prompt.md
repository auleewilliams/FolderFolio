# FolderFolio — build the MVP

Build a self-hosted photo portfolio web app called **FolderFolio**.

## Core concept

The filesystem is the CMS. There is no admin panel, no upload UI, and no
login. I copy photos into folders on the server; the app scans those
folders and renders a public portfolio site from them.

## Stack

- ASP.NET Core **Razor Pages** (not Blazor, not an SPA) on the current
  .NET LTS
- **SixLabors.ImageSharp** for all image processing.
  Do NOT use `System.Drawing` — it isn't supported on Linux.
- No database. Hold the index in memory and rebuild it on startup.
- Deployed as a Linux container.

## Folder layout

The photo root is configurable. Inside it, one folder per album:

```
/photos/
  01-Landscapes/
    DSC_0001.jpg
    DSC_0002.jpg
  02-Portraits/
    ...
```

Rules:
- Each top-level folder is one album. Ignore nested subfolders for the MVP.
- Album display title comes from the folder name: strip a leading numeric
  sort prefix (`01-`, `02_`) and replace dashes/underscores with spaces.
  Use the numeric prefix, when present, to order albums; fall back to
  alphabetical.
- Album slug is a URL-safe version of the title.
- Supported extensions for the MVP: `.jpg`, `.jpeg`, `.png`, `.webp`.
  Skip anything else silently.
- Photos within an album sort by EXIF capture date, falling back to
  filename when EXIF is missing.
- Album cover is the first photo in sort order.

## Indexing

- A `BackgroundService` scans the photo root on startup and builds the
  in-memory index (albums, photos, EXIF capture date, file size,
  dimensions, last-write-time).
- A `FileSystemWatcher` on the photo root updates the index when files or
  folders are added, removed, or renamed. Debounce the events — bulk
  copies fire many at once. A short debounce followed by a targeted
  rescan of the affected album is fine.
- The index must be safe to read from request threads while the scanner
  writes to it. Swap in an immutable snapshot rather than mutating shared
  state in place.
- Log a summary after each scan (albums found, photos found, time taken).

## Thumbnails

- Generate two derivative sizes on first request: a grid thumbnail
  (~400px on the long edge) and a web-view size (~2000px on the long
  edge). Both sizes configurable.
- Cache derivatives to a configurable cache directory on disk. The cache
  key must include the source path **and** its last-write-time so
  replacing a photo invalidates the old derivative automatically.
- Strip EXIF from generated derivatives (this includes GPS coordinates,
  which should not be served publicly). Preserve orientation by baking
  the EXIF rotation into the output.
- Encode derivatives as WebP with a configurable quality.
- Guard against a thundering herd: concurrent requests for the same
  uncached derivative should generate it once, not N times.

## Serving images

The photo root lives outside `wwwroot`, so images are served through a
controller/endpoint, not static file middleware. The endpoint takes an
album slug, a photo identifier, and a size, resolves it against the
index, and streams the cached derivative.

Important: validate that the resolved path sits inside the configured
photo root before reading anything. Never build a path directly from
user input — resolve through the in-memory index only. Return 404 for
anything not in the index.

Serve derivatives with long-lived cache headers and an ETag, since the
cache key already changes when the source changes.

## Pages

1. `/` — album grid. Cover image, album title, photo count per album.
2. `/albums/{slug}` — responsive photo grid for one album, with a back
   link to the index.
3. Clicking a photo opens a lightbox showing the web-size derivative,
   with next/previous navigation, keyboard support (arrows and Escape),
   and a close control. Hand-roll this or use a small dependency — no
   heavy frameworks.

Design notes: this is a photography portfolio, so the chrome should stay
out of the way. Dark neutral background, generous whitespace, minimal
typography, images as the focus. Use `loading="lazy"` on grid images and
set width/height or aspect-ratio to avoid layout shift.

## Configuration

Bind a strongly-typed options class from `appsettings.json` plus
environment variable overrides:

- Photo root path
- Thumbnail cache path
- Thumbnail and web-view dimensions
- WebP quality
- Site title and a short tagline for the homepage

## Deployment

- Multi-stage `Dockerfile` producing a small runtime image.
- `docker-compose.yml` with volumes for the photo directory and the
  thumbnail cache.
- The app runs behind a Cloudflare Tunnel, so configure
  `ForwardedHeadersMiddleware` to honour `X-Forwarded-Proto` and
  `X-Forwarded-For` so generated URLs use the correct scheme.
- Add a `/health` endpoint that reports index status.

## What to deliver

- Full solution structure with the projects laid out sensibly
- All source files, complete and compiling
- `appsettings.json` with sane defaults
- `Dockerfile` and `docker-compose.yml`
- A `README.md` covering setup, configuration, and the folder convention

## Constraints

- Keep the MVP scope tight. No auth, no upload, no tagging, no search,
  no EXIF display on the front end, no nested albums. Note anything you
  deliberately left out at the end.
- Prefer clear, idiomatic code over cleverness.
- Explain any non-obvious design decisions briefly in comments.