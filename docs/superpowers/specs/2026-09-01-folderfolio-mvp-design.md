# FolderFolio MVP Design

Date: 2026-09-01  
Status: Approved in conversation; awaiting written-spec review

## 1. Purpose and scope

FolderFolio is a self-hosted public photography portfolio whose filesystem is
the content-management system. An operator adds, replaces, renames, or removes
photos in album folders on the server. The application indexes those folders
and renders a portfolio without a database, administration interface, upload
flow, or login.

The MVP will use ASP.NET Core Razor Pages on .NET 10 LTS and
SixLabors.ImageSharp. It will run as a single Linux container instance. The
design deliberately optimizes for a small self-hosted deployment rather than a
multi-instance cluster.

The MVP includes:

- a public album index;
- a public album gallery;
- an accessible lightbox;
- startup indexing and debounced filesystem updates;
- on-demand privacy-safe WebP derivatives;
- a health endpoint;
- container deployment assets; and
- automated unit and integration tests for the important behavior.

It excludes authentication, uploads, search, tags, captions, frontend EXIF,
nested albums, focal-point editing, cache eviction, periodic full
reconciliation, slideshows, swipe gestures, and browser-automation tests.

## 2. Selected architecture

FolderFolio will be a modular monolith: one deployable Razor Pages project and
one xUnit test project. Production code stays in one assembly, but folders,
interfaces, and dependency-injection boundaries separate the main concerns.
This provides enough isolation for safe testing without creating the ceremony
of a multi-project clean architecture.

Proposed solution layout:

```text
FolderFolio.slnx
src/
  FolderFolio/
    Configuration/
    Domain/
    Indexing/
    Imaging/
    Pages/
    Web/
    wwwroot/
    Program.cs
    appsettings.json
tests/
  FolderFolio.Tests/
Dockerfile
docker-compose.yml
README.md
```

The internal modules have these responsibilities:

- **Configuration** binds and validates application settings.
- **Domain** defines immutable albums, photos, snapshots, and index health.
- **Indexing** scans the filesystem, extracts source metadata, coordinates
  watcher events, and atomically publishes snapshots.
- **Imaging** calculates cache identities and generates WebP derivatives.
- **Pages** renders the portfolio and album routes with Razor Pages.
- **Web** resolves media requests, reports health, and contains small web-only
  helpers such as versioned image URL creation.

## 3. Configuration

A strongly typed `FolderFolioOptions` object will bind the `FolderFolio`
section from `appsettings.json`. Standard ASP.NET Core configuration allows
environment variables to override values with the `FolderFolio__...` naming
form.

The public configuration surface is:

| Setting | Default | Validation |
| --- | --- | --- |
| `PhotoRoot` | `/photos` | Required absolute path |
| `CacheRoot` | `/cache` | Required absolute path |
| `GridLongEdge` | `400` | Positive integer |
| `WebLongEdge` | `2000` | Positive and at least `GridLongEdge` |
| `WebPQuality` | `82` | Integer from 1 through 100 |
| `SiteTitle` | `FolderFolio` | Required nonblank text |
| `Tagline` | `Photos from a folder.` | Text; may be blank |

Watcher debounce and startup retry intervals remain internal constants for the
MVP. Changing configuration requires an application restart. Derivative cache
identities include all image-output settings, so a restart with new dimensions
or quality generates new derivative URLs automatically.

## 4. Filesystem and naming rules

The scanner treats each immediate directory under `PhotoRoot` as one album and
each supported regular file immediately inside that directory as one photo.
It ignores nested directories and files with extensions other than `.jpg`,
`.jpeg`, `.png`, and `.webp`, case-insensitively.

For safety and predictable containment, the MVP skips symbolic links and other
filesystem reparse points for both albums and photos. It also skips unreadable,
incomplete, or corrupt images independently, logging a warning without
discarding other valid content.

Album names use these transformations:

1. A leading run of digits followed by `-` or `_` is parsed as the optional
   numeric order and removed from the title. The prefix is only recognized when
   text follows it.
2. Remaining runs of dashes and underscores become a single space.
3. Repeated whitespace is collapsed and the result is trimmed.
4. The URL slug is a lowercase, URL-safe ASCII form of the title. A title that
   produces no ASCII characters falls back to `album`.
5. Slug collisions receive a deterministic short hash suffix derived from the
   original album directory name.

Albums with numeric prefixes sort first by their numeric value. Albums without
a prefix follow alphabetically. Ties use display title and then original
directory name, both with deterministic ordinal comparisons.

For photos, the scanner reads EXIF `DateTimeOriginal`, then
`DateTimeDigitized`. EXIF values are compared as the camera recorded them; no
timezone conversion is attempted because common camera EXIF does not contain a
reliable zone. Dated photos sort chronologically, with filename as a tie-breaker.
Undated photos follow in case-insensitive filename order. The first resulting
photo is the album cover.

Each photo receives an opaque identifier from the SHA-256 hash of its normalized
photo-root-relative path. Renaming a file therefore changes its identifier.
Replacing a file at the same path retains the identifier but changes its source
version and all derivative URLs.

## 5. Immutable index and scan lifecycle

Request code reads a single immutable `PortfolioSnapshot`. The snapshot contains
the ordered album list and lookup maps for album slugs and photo identifiers.
An album and photo may retain absolute source paths internally, but those paths
are never rendered or accepted as request values.

The index store publishes an immutable `IndexPublication` atomically. It contains:

- the current snapshot;
- a monotonically increasing generation number;
- `Starting`, `Ready`, or `Degraded` status;
- the last successful scan time and duration;
- album and photo totals; and
- a sanitized last-error summary.

Requests capture one publication reference and see either the complete old
state or the complete new state. No request can observe a snapshot while it is
being mutated.

At startup, the background indexing service checks that the configured root is
available, enables its watcher, and then performs a full scan. Watcher events
that arrive during the scan are queued for subsequent processing. If the root
does not exist or cannot be read, the service remains in `Starting`, retries the
availability check after a short fixed interval, and leaves `/health` unhealthy.
Once the root becomes available, watcher setup precedes the scan so changes
cannot fall into a gap. A successful scan publishes `Ready`, even when the root
contains no albums.

After startup, filesystem events enter a bounded single-reader channel. The
coordinator coalesces duplicate events and waits for a short quiet period before
rescanning an affected album. Album-directory additions, removals, and renames
trigger a full scan because global ordering and slug collisions can change.
Photo changes trigger a targeted album rescan followed by construction of a new
complete snapshot. A photo rename or move that crosses album boundaries targets
both its old and new albums.

`FileSystemWatcher` can duplicate, reorder, or lose events. A watcher error,
internal queue overflow, or event that cannot be mapped safely to one album
schedules a full scan. If a later scan fails, the service publishes `Degraded`
while retaining the last valid snapshot for visitors. There is no periodic full
reconciliation in the MVP.

Every scan logs its kind, album count, photo count, skipped-file count, and
elapsed time. Logs and health output do not reveal absolute filesystem paths to
public clients.

## 6. Path security and media resolution

User input is never concatenated into a filesystem path. A media request
contains only an album slug, opaque photo identifier, derivative size, and
version. The endpoint resolves the album and photo through lookup maps in its
captured immutable snapshot.

Before reading an indexed source, the imaging service defensively obtains its
full path again, verifies that it remains under the configured canonical photo
root, verifies that it is still a supported regular non-link file, and compares
its current length and last-write timestamp with the indexed source version.
A mismatch returns `404` and schedules an album rescan. This defends the request
boundary without pretending that an administrator with write access to the
photo root is an untrusted tenant.

Unknown slugs, photo identifiers, derivative sizes, stale versions, and missing
sources return `404` without leaking paths or exception details.

## 7. Derivative generation and caching

Two derivative kinds are supported:

- `grid`, constrained to `GridLongEdge`; and
- `web`, constrained to `WebLongEdge`.

The service never enlarges a source whose long edge is already smaller than the
requested maximum. It uses ImageSharp to decode, apply EXIF orientation to the
pixels, resize while preserving aspect ratio, remove EXIF/IPTC/XMP metadata, and
encode WebP at the configured quality. Removing metadata prevents GPS and other
private camera data from reaching the public derivative.

A cache identity hashes:

- a cache-schema version;
- normalized source-relative path;
- indexed last-write timestamp and byte length;
- derivative kind and long-edge dimension;
- WebP quality; and
- output format.

The browser-facing media URL is:

```text
/media/{albumSlug}/{photoId}/{size}?v={cacheVersion}
```

The version is derived from the same cache identity. A request whose version is
not current receives `404`. Successful responses include
`Cache-Control: public, max-age=31536000, immutable` and an ETag derived from the
full identity. Replacing a source or changing output configuration produces a
new URL and therefore cannot be hidden behind a stale browser or CDN entry.

For an uncached derivative, a process-local asynchronous single-flight map
allows only one generation task per cache identity. Waiters share its result.
The service checks the cache again after entering the single-flight operation,
writes to a unique temporary file in the destination directory, checks that the
source version has not changed during processing, and atomically moves the file
into place. Faulted and completed entries are removed from the in-memory map.
No request can stream a partially written cache file.

Old versioned derivatives are intentionally left on disk in the MVP. Operators
may clear the cache safely while the app is stopped; missing derivatives are
regenerated on demand.

## 8. Web pages and interaction design

The interface uses a near-black neutral background, warm light text, restrained
system typography, generous spacing, and no decorative framework. The configured
site title and tagline appear on the home page.

`/` renders:

- a `Preparing portfolio` state before the first successful scan;
- a concise empty-library state after a successful scan with no albums; or
- a responsive album grid with a consistently cropped cover, album title, and
  photo count.

`/albums/{slug}` returns `404` for an unknown album and otherwise renders a back
link, album title, photo count, and responsive photo grid. Gallery thumbnails
preserve their natural aspect ratios rather than cropping the source. Indexed
width and height are emitted on every image to reserve layout space. Below-fold
images use `loading="lazy"` and `decoding="async"`; the first visible images are
loaded eagerly.

Each gallery item is a keyboard-operable button that opens a native HTML
`<dialog>` lightbox. The dialog displays the `web` derivative with
`object-fit: contain`, previous and next controls, and an explicit close control.
Left and Right arrows navigate, Escape closes, opening moves focus into the
dialog, and closing returns focus to the triggering gallery item. Navigation
controls have descriptive accessible names and at least 44-pixel touch targets.
Animations are subtle and disabled when `prefers-reduced-motion` is active.

Since the MVP has no captions, gallery controls use generic accessible labels
such as `Photo 3 of 12 in Landscapes`; filenames are not exposed as presentation
copy. Album covers use labels such as `Landscapes album cover`.

A neutral reserved surface avoids layout jumps while derivatives load. A failed
grid image receives a quiet unavailable treatment. A lightbox load failure keeps
the close and navigation controls usable and displays a retryable error without
revealing server details.

The frontend contains only page-scoped vanilla JavaScript for the lightbox. It
does not compose media paths; Razor supplies fully encoded current media URLs in
`data-*` attributes.

## 9. Health, proxying, and deployment

`GET /health` returns a small JSON document containing index status, generation,
album and photo totals, and last successful scan time. It returns `503` while
startup has not produced a valid snapshot and while the service is degraded;
otherwise it returns `200`. The public portfolio may continue serving the last
valid snapshot while health is degraded.

Forwarded Headers Middleware runs early and processes only
`X-Forwarded-For` and `X-Forwarded-Proto`, with a forward limit of one. The
provided Compose configuration binds the origin port to `127.0.0.1` so it is not
directly exposed to remote clients; this makes accepting the local tunnel's
forwarded values safe under the documented deployment model. The README will
warn operators to configure explicit trusted proxies before exposing the origin
on any non-loopback interface.

The multi-stage Dockerfile builds with the .NET 10 SDK and runs on the matching
ASP.NET Core runtime image. The process listens on port 8080 and runs without
root privileges. Compose mounts the photo directory read-only and the derivative
cache read-write, provides environment-variable examples, and publishes only
`127.0.0.1:8080:8080` for a host-based Cloudflare Tunnel.

## 10. Error handling

Expected content problems are isolated to the smallest useful boundary:

- a corrupt photo is skipped;
- an album that disappears during a scan is omitted from the new snapshot;
- a source that changes during derivative generation does not publish a stale
  cache entry;
- an invalid media request returns `404`;
- a failed derivative returns a generic server error and remains retryable; and
- an indexing failure retains the last valid snapshot while health becomes
  degraded.

Unexpected exceptions are logged with server-side context and handled by the
standard production exception page. Public responses never include stack traces
or filesystem paths.

## 11. Test strategy and acceptance criteria

The xUnit project will use temporary directories and generated ImageSharp
fixtures. `Microsoft.AspNetCore.Mvc.Testing` will exercise endpoints and rendered
page states. Watcher coordination will be tested through an injected event
abstraction rather than relying on nondeterministic operating-system timing.

Automated coverage includes:

- album title parsing, ordering, slug creation, and slug collisions;
- photo EXIF ordering, filename fallback, and stable identifiers;
- extension, nesting, corrupt-file, and link filtering;
- atomic publication under concurrent readers;
- full and targeted rescan behavior, event debouncing, and overflow recovery;
- path containment and lookup-only media resolution;
- grid and web dimensions, no upscaling, orientation baking, and metadata removal;
- cache identity changes after source or configuration changes;
- one physical generation under simultaneous first requests;
- no partially visible cache files;
- startup, ready, and degraded health transitions;
- media content type, ETag, immutable cache headers, stale-version rejection, and
  generic `404` handling; and
- preparing, empty, populated, album, and missing-album page states.

The completion gate is:

1. `dotnet test` succeeds from the repository root.
2. `dotnet publish -c Release` succeeds.
3. The container image builds successfully.
4. A manual smoke run against temporary albums confirms filesystem updates,
   derivative generation, page navigation, keyboard lightbox behavior, and
   health output.
5. The README documents setup, mounts, environment overrides, folder naming,
   Cloudflare Tunnel assumptions, cache behavior, and deliberate MVP omissions.
