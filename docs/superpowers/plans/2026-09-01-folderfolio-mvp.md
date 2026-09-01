# FolderFolio MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a complete, self-hosted photo portfolio whose external photo directory is its CMS, with safe indexing, privacy-preserving derivatives, Razor Pages, and Linux container deployment.

**Architecture:** Implement one modular ASP.NET Core Razor Pages application and one xUnit test project. A single background writer publishes immutable index snapshots; request code reads snapshots, resolves opaque identifiers, and delegates derivative creation to a versioned, single-flight disk cache.

**Tech Stack:** .NET 10 LTS, ASP.NET Core Razor Pages, C# 14, SixLabors.ImageSharp 4.1.1, xUnit.net v3 4.0.0, vanilla JavaScript/CSS, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-09-01-folderfolio-mvp-design.md`

## Global Constraints

- Target `net10.0`; do not add Blazor, SPA tooling, a database, authentication, or an upload interface.
- Pin `SixLabors.ImageSharp` to `4.1.1`; do not use `System.Drawing` or ImageSharp.Web.
- Treat only immediate child directories as albums and only immediate supported image files as photos.
- Supported extensions are `.jpg`, `.jpeg`, `.png`, and `.webp`, case-insensitively.
- Skip nested content, symbolic links/reparse points, unsupported files, and corrupt images.
- Publish complete immutable snapshots atomically; request paths never observe mutation in progress.
- Treat route values only as lookup keys; never concatenate user input into a source filesystem path.
- Bake orientation into derivatives and emit WebP without EXIF, IPTC, or XMP metadata.
- Preserve the last successful snapshot after later indexing failures and report degraded health as HTTP `503`.
- Run as one non-root Linux container; mount photos read-only and derivative cache read-write.
- Keep the MVP omissions in the approved spec out of implementation.

---

## File map

### Repository and build

- `global.json` — pins the .NET 10 SDK feature band while allowing later compatible feature bands.
- `Directory.Packages.props` — centrally pins every NuGet dependency.
- `FolderFolio.slnx` — contains the application and test projects.
- `Dockerfile`, `docker-compose.yml`, `.dockerignore` — container build and local/tunnel deployment.
- `README.md` — operator setup, configuration, folder rules, proxy assumptions, and omissions.

### Application

- `src/FolderFolio/Configuration/FolderFolioOptions.cs` — strongly typed settings and defaults.
- `src/FolderFolio/Configuration/FolderFolioOptionsValidator.cs` — startup validation.
- `src/FolderFolio/Domain/*.cs` — immutable album, photo, fingerprint, snapshot, and health records.
- `src/FolderFolio/Indexing/AlbumNameParser.cs` — display-title, sort-prefix, and base-slug rules.
- `src/FolderFolio/Indexing/AlbumCatalogBuilder.cs` — deterministic album ordering and slug collision allocation.
- `src/FolderFolio/Indexing/PhotoIdentity.cs` — opaque stable photo identifiers.
- `src/FolderFolio/Indexing/ImageSharpMetadataReader.cs` — dimensions and EXIF capture-date extraction without pixel decode.
- `src/FolderFolio/Indexing/PhotoScanner.cs` — full and targeted filesystem scans.
- `src/FolderFolio/Indexing/PortfolioIndex.cs` — atomic publication and status transitions.
- `src/FolderFolio/Indexing/IndexRefreshQueue.cs` — bounded watcher-event queue with overflow escalation.
- `src/FolderFolio/Indexing/IndexRefreshCoordinator.cs` — quiet-period coalescing and scan publication.
- `src/FolderFolio/Indexing/FileSystemPhotoRootWatcher.cs` — thin `FileSystemWatcher` adapter.
- `src/FolderFolio/Indexing/IndexingService.cs` — startup retry, watcher lifecycle, and background loop.
- `src/FolderFolio/Imaging/DerivativeKeyFactory.cs` — cache key, URL version, and ETag identity.
- `src/FolderFolio/Imaging/SourcePathGuard.cs` — trusted-root containment and fingerprint validation.
- `src/FolderFolio/Imaging/ImageSharpDerivativeGenerator.cs` — orientation, resize, metadata stripping, WebP encode.
- `src/FolderFolio/Imaging/DerivativeService.cs` — disk cache, atomic writes, and single-flight coordination.
- `src/FolderFolio/Web/MediaEndpoint.cs` — lookup-only image delivery and cache headers.
- `src/FolderFolio/Web/MediaUrlBuilder.cs` — route-generated, versioned media URLs.
- `src/FolderFolio/Web/HealthEndpoint.cs` — index-aware JSON health result.
- `src/FolderFolio/Web/ForwardedHeadersSetup.cs` — one-hop tunnel proxy policy.
- `src/FolderFolio/Web/ViewModels/*.cs` — filesystem-free page presentation records and mapping.
- `src/FolderFolio/Pages/*.cshtml*` — home, album, shared layout, lightbox, and error pages.
- `src/FolderFolio/wwwroot/css/site.css` — restrained responsive portfolio presentation.
- `src/FolderFolio/wwwroot/js/lightbox.js` — accessible native-dialog interaction.
- `src/FolderFolio/Program.cs` — dependency registration, middleware order, and endpoint mapping.

### Tests

- `tests/FolderFolio.Tests/Support/*` — temporary-directory, image-fixture, scanner, generator, and web-host fakes.
- `tests/FolderFolio.Tests/Configuration/*` — options validation.
- `tests/FolderFolio.Tests/Indexing/*` — naming, scanning, immutable publication, watcher mapping, and refresh coordination.
- `tests/FolderFolio.Tests/Imaging/*` — keying, containment, WebP privacy, atomic cache, and concurrency.
- `tests/FolderFolio.Tests/Web/*` — media, pages, health, and forwarded-header configuration.

---

### Task 1: Scaffold the solution and validate configuration

**Files:**
- Create: `global.json`
- Create: `Directory.Packages.props`
- Create: `FolderFolio.slnx`
- Create: `src/FolderFolio/FolderFolio.csproj`
- Create: `src/FolderFolio/Program.cs`
- Create: `src/FolderFolio/appsettings.json`
- Create: `src/FolderFolio/Configuration/FolderFolioOptions.cs`
- Create: `src/FolderFolio/Configuration/FolderFolioOptionsValidator.cs`
- Create: `tests/FolderFolio.Tests/FolderFolio.Tests.csproj`
- Create: `tests/FolderFolio.Tests/Configuration/FolderFolioOptionsTests.cs`

**Interfaces:**
- Produces: `FolderFolioOptions.SectionName`, validated settings, a buildable `FolderFolio.slnx`, and `public partial class Program` for integration tests.

- [ ] **Step 1: Create the solution and centrally pinned project files**

Use `apply_patch` to create the files. `Directory.Packages.props` must pin:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.11" />
    <PackageVersion Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.9.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.9.0" />
    <PackageVersion Include="SixLabors.ImageSharp" Version="4.1.1" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="4.0.0" />
    <PackageVersion Include="xunit.v3" Version="4.0.0" />
  </ItemGroup>
</Project>
```

`global.json` is:

```json
{
  "sdk": {
    "version": "10.0.400",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

The web project is:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="SixLabors.ImageSharp" />
  </ItemGroup>
</Project>
```

The test project uses this complete structure:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/FolderFolio/FolderFolio.csproj" />
  </ItemGroup>
</Project>
```

`FolderFolio.slnx` is:

```xml
<Solution>
  <Project Path="src/FolderFolio/FolderFolio.csproj" />
  <Project Path="tests/FolderFolio.Tests/FolderFolio.Tests.csproj" />
</Solution>
```

- [ ] **Step 2: Restore and verify the pinned dependency set**

Run `dotnet restore FolderFolio.slnx` without ignored sources. Confirm it
creates one `packages.lock.json` beside each project, then run `dotnet list
FolderFolio.slnx package` and verify the six direct package versions match
`Directory.Packages.props`. Keep both lock files and use locked restore after
this task.

- [ ] **Step 3: Write the failing options tests**

```csharp
public sealed class FolderFolioOptionsTests
{
    [Fact]
    public void Defaults_match_the_container_contract()
    {
        var options = new FolderFolioOptions();

        Assert.Equal("/photos", options.PhotoRoot);
        Assert.Equal("/cache", options.CacheRoot);
        Assert.Equal(400, options.GridLongEdge);
        Assert.Equal(2000, options.WebLongEdge);
        Assert.Equal(82, options.WebPQuality);
        Assert.Equal("FolderFolio", options.SiteTitle);
    }

    [Fact]
    public void Validator_rejects_invalid_paths_sizes_quality_and_title()
    {
        var options = new FolderFolioOptions
        {
            PhotoRoot = "relative/photos",
            CacheRoot = " ",
            GridLongEdge = 0,
            WebLongEdge = -1,
            WebPQuality = 101,
            SiteTitle = " "
        };

        var result = new FolderFolioOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, value => value.Contains(nameof(options.PhotoRoot)));
        Assert.Contains(result.Failures, value => value.Contains(nameof(options.CacheRoot)));
        Assert.Contains(result.Failures, value => value.Contains(nameof(options.GridLongEdge)));
        Assert.Contains(result.Failures, value => value.Contains(nameof(options.WebPQuality)));
        Assert.Contains(result.Failures, value => value.Contains(nameof(options.SiteTitle)));
    }
}
```

- [ ] **Step 4: Run the configuration tests and confirm the red state**

Run: `dotnet test FolderFolio.slnx --filter FullyQualifiedName~FolderFolioOptionsTests`

Expected: FAIL at compile time because `FolderFolioOptions` and its validator do not yet exist.

- [ ] **Step 5: Implement options, validator, defaults, and minimal startup**

`FolderFolioOptions` exposes writable properties named exactly as the tests and
spec require. `FolderFolioOptionsValidator : IValidateOptions<FolderFolioOptions>`
returns one failure message per invalid property, additionally requiring
`WebLongEdge >= GridLongEdge`. Use this class shape:

```csharp
public sealed class FolderFolioOptions
{
    public const string SectionName = "FolderFolio";
    public string PhotoRoot { get; set; } = "/photos";
    public string CacheRoot { get; set; } = "/cache";
    public int GridLongEdge { get; set; } = 400;
    public int WebLongEdge { get; set; } = 2000;
    public int WebPQuality { get; set; } = 82;
    public string SiteTitle { get; set; } = "FolderFolio";
    public string Tagline { get; set; } = "Photos from a folder.";
}
```

Register it with:

```csharp
builder.Services.AddSingleton<IValidateOptions<FolderFolioOptions>, FolderFolioOptionsValidator>();
builder.Services.AddOptions<FolderFolioOptions>()
    .Bind(builder.Configuration.GetSection(FolderFolioOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddRazorPages();
```

The initial `Program.cs` uses static files, routing, and `MapRazorPages()`, then
ends with `public partial class Program { }`. Put the seven defaults from the
spec under a `FolderFolio` object in `appsettings.json`:

```json
{
  "FolderFolio": {
    "PhotoRoot": "/photos",
    "CacheRoot": "/cache",
    "GridLongEdge": 400,
    "WebLongEdge": 2000,
    "WebPQuality": 82,
    "SiteTitle": "FolderFolio",
    "Tagline": "Photos from a folder."
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 6: Run the solution tests and build**

Run: `dotnet test FolderFolio.slnx`

Expected: PASS with 2 tests.

Run: `dotnet build FolderFolio.slnx --no-restore`

Expected: build succeeds with zero warnings.

- [ ] **Step 7: Commit the scaffold**

```bash
git add global.json Directory.Packages.props FolderFolio.slnx src tests
git commit -m "build: scaffold FolderFolio on .NET 10"
```

---

### Task 2: Implement immutable domain models, naming, identifiers, and publication

**Files:**
- Create: `src/FolderFolio/Domain/SourceFingerprint.cs`
- Create: `src/FolderFolio/Domain/IndexedPhoto.cs`
- Create: `src/FolderFolio/Domain/ScannedAlbum.cs`
- Create: `src/FolderFolio/Domain/IndexedAlbum.cs`
- Create: `src/FolderFolio/Domain/PortfolioSnapshot.cs`
- Create: `src/FolderFolio/Domain/IndexStatus.cs`
- Create: `src/FolderFolio/Domain/IndexPublication.cs`
- Create: `src/FolderFolio/Indexing/AlbumNameInfo.cs`
- Create: `src/FolderFolio/Indexing/AlbumNameParser.cs`
- Create: `src/FolderFolio/Indexing/AlbumCatalogBuilder.cs`
- Create: `src/FolderFolio/Indexing/PhotoIdentity.cs`
- Create: `src/FolderFolio/Indexing/IPortfolioIndex.cs`
- Create: `src/FolderFolio/Indexing/PortfolioIndex.cs`
- Create: `tests/FolderFolio.Tests/Indexing/AlbumNameParserTests.cs`
- Create: `tests/FolderFolio.Tests/Indexing/AlbumCatalogBuilderTests.cs`
- Create: `tests/FolderFolio.Tests/Indexing/PhotoIdentityTests.cs`
- Create: `tests/FolderFolio.Tests/Indexing/PortfolioIndexTests.cs`

**Interfaces:**
- Produces: `AlbumNameParser.Parse(string)`, `AlbumCatalogBuilder.Build(IEnumerable<ScannedAlbum>)`, `PhotoIdentity.FromRelativePath(string)`, and `IPortfolioIndex.Current` plus atomic status-transition methods.

- [ ] **Step 1: Write failing naming, collision, ordering, and identity tests**

Cover these exact examples:

```csharp
[Theory]
[InlineData("01-Landscapes_and-Sea", 1, "Landscapes and Sea", "landscapes-and-sea")]
[InlineData("Portraits", null, "Portraits", "portraits")]
[InlineData("猫", null, "猫", "album")]
public void Parses_album_directory_names(string input, int? order, string title, string slug)
{
    var result = AlbumNameParser.Parse(input);
    Assert.Equal(order, result.SortPrefix);
    Assert.Equal(title, result.Title);
    Assert.Equal(slug, result.BaseSlug);
}
```

Build two albums named `01-Summer` and `02_Summer`, assert both slugs start
with `summer--`, differ from each other, and map to the same folder after input
order is reversed. Assert prefixed albums sort numerically before unprefixed
albums. Assert photo identity treats `Album\\Photo.jpg` and `Album/Photo.jpg`
as the same relative path while a rename changes the ID.

- [ ] **Step 2: Run the naming tests and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter "FullyQualifiedName~AlbumNameParserTests|FullyQualifiedName~AlbumCatalogBuilderTests|FullyQualifiedName~PhotoIdentityTests"`

Expected: FAIL because the domain and indexing types are absent.

- [ ] **Step 3: Implement the immutable records and catalog rules**

Use these record shapes:

```csharp
public sealed record AlbumNameInfo(int? SortPrefix, string Title, string BaseSlug);

public sealed record SourceFingerprint(string RelativePath, long Length, long LastWriteUtcTicks);

public sealed record IndexedPhoto(
    string Id,
    string FileName,
    SourceFingerprint Source,
    DateTime? CapturedAt,
    int Width,
    int Height);

public sealed record ScannedAlbum(
    string DirectoryName,
    string Title,
    string BaseSlug,
    int? SortPrefix,
    ImmutableArray<IndexedPhoto> Photos);

public sealed record IndexedAlbum(
    string DirectoryName,
    string Slug,
    string Title,
    string BaseSlug,
    int? SortPrefix,
    ImmutableArray<IndexedPhoto> Photos)
{
    public IndexedPhoto? Cover => Photos.IsDefaultOrEmpty ? null : Photos[0];
}
```

`PortfolioSnapshot` stores ordered `ImmutableArray<IndexedAlbum> Albums` and an
ordinal-ignore-case `ImmutableDictionary<string, IndexedAlbum> AlbumsBySlug`.
It exposes `Empty`, `AlbumCount`, and `PhotoCount`.

`AlbumNameParser` uses `^(?<order>\d+)[-_](?<title>.+)$`, replaces runs of
`-`, `_`, and whitespace with one space for display, strips Unicode combining
marks, retains ASCII letters/digits for slugs, collapses other runs to one
hyphen, and falls back to display title `Album` and slug `album` when cleaning
would otherwise produce empty text.

`AlbumCatalogBuilder` groups by `BaseSlug` with ordinal-ignore-case comparison.
Every member of a colliding group receives
`{baseSlug}--{firstEightLowercaseSha256HexOfDirectoryName}`. It sorts prefixed
albums first by numeric prefix, then title and directory name; unprefixed albums
follow by title and directory name.

`PhotoIdentity` replaces backslashes with forward slashes and returns the full
lowercase SHA-256 hex digest of the normalized relative path.

- [ ] **Step 4: Write failing atomic-publication tests**

Create old and new snapshots whose album title and photo count differ. Run one
writer publishing them repeatedly while several readers capture `Current`.
Assert every captured publication is internally one complete old or new value,
never a mixed pair. Also assert `MarkDegraded("Scan failed")` preserves the last
ready snapshot.

- [ ] **Step 5: Run publication tests and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter FullyQualifiedName~PortfolioIndexTests`

Expected: FAIL because `PortfolioIndex` is absent.

- [ ] **Step 6: Implement the atomic index store**

```csharp
public interface IPortfolioIndex
{
    IndexPublication Current { get; }
    void PublishReady(PortfolioSnapshot snapshot, DateTimeOffset completedAtUtc, TimeSpan duration);
    void MarkStarting(string? publicError = null);
    void MarkDegraded(string publicError);
}
```

`IndexPublication` contains generation, `IndexStatus` (`Starting`, `Ready`,
`Degraded`), snapshot, nullable last-success time and duration, and nullable
sanitized error. `PortfolioIndex` stores one reference, reads with
`Volatile.Read`, increments generation with `Interlocked.Increment`, and swaps a
new complete publication with `Interlocked.Exchange`.

- [ ] **Step 7: Run task tests and commit**

Run: `dotnet test FolderFolio.slnx --filter "FullyQualifiedName~Indexing"`

Expected: PASS.

```bash
git add src/FolderFolio/Domain src/FolderFolio/Indexing tests/FolderFolio.Tests/Indexing
git commit -m "feat: add immutable portfolio index model"
```

---

### Task 3: Scan albums and photo metadata safely

**Files:**
- Create: `src/FolderFolio/Indexing/PhotoSourceMetadata.cs`
- Create: `src/FolderFolio/Indexing/PhotoScanResult.cs`
- Create: `src/FolderFolio/Indexing/IImageMetadataReader.cs`
- Create: `src/FolderFolio/Indexing/ImageSharpMetadataReader.cs`
- Create: `src/FolderFolio/Indexing/IPhotoScanner.cs`
- Create: `src/FolderFolio/Indexing/PhotoScanner.cs`
- Create: `tests/FolderFolio.Tests/Support/TemporaryDirectory.cs`
- Create: `tests/FolderFolio.Tests/Support/ImageFixtureFactory.cs`
- Create: `tests/FolderFolio.Tests/Indexing/ImageSharpMetadataReaderTests.cs`
- Create: `tests/FolderFolio.Tests/Indexing/PhotoScannerTests.cs`

**Interfaces:**
- Consumes: configuration, `AlbumNameParser`, `AlbumCatalogBuilder`, `PhotoIdentity`, and immutable snapshots.
- Produces: `IImageMetadataReader.IdentifyAsync`, `IPhotoScanner.ScanAllAsync`, and `IPhotoScanner.RescanAlbumsAsync`.

- [ ] **Step 1: Write failing ImageSharp metadata tests**

`ImageFixtureFactory` creates a small JPEG using `Image<Rgba32>`, adds
`ExifTag.DateTimeOriginal` as `yyyy:MM:dd HH:mm:ss`, and saves it. Assert:

```csharp
var metadata = await reader.IdentifyAsync(path, TestContext.Current.CancellationToken);

Assert.Equal(120, metadata.Width);
Assert.Equal(80, metadata.Height);
Assert.Equal(new DateTime(2024, 3, 2, 10, 11, 12), metadata.CapturedAt);
Assert.True(metadata.EstimatedPixelBytes > 0);
```

Add a fixture containing only `DateTimeDigitized` and one with malformed EXIF;
the former is parsed and the latter returns a null capture date. Add a
`120x80` fixture with EXIF orientation value 6 and assert the returned logical
display dimensions are `80x120`.

- [ ] **Step 2: Run metadata tests and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter FullyQualifiedName~ImageSharpMetadataReaderTests`

Expected: FAIL because the reader is absent.

- [ ] **Step 3: Implement metadata identification**

Use ImageSharp 4.1.1 with:

```csharp
var options = new DecoderOptions
{
    MaxFrames = 1,
    SkipMetadata = false,
    SegmentIntegrityHandling = SegmentIntegrityHandling.Strict
};
var info = await Image.IdentifyAsync(options, path, cancellationToken);
```

Use these exact contracts:

```csharp
public sealed record PhotoSourceMetadata(
    int Width,
    int Height,
    DateTime? CapturedAt,
    long EstimatedPixelBytes);

public interface IImageMetadataReader
{
    Task<PhotoSourceMetadata> IdentifyAsync(
        string sourcePath,
        CancellationToken cancellationToken);
}
```

Read `DateTimeOriginal`, then `DateTimeDigitized`, through
`ExifProfile.TryGetValue`. Parse exactly with invariant culture and
`DateTimeStyles.None`. Read `ExifTag.Orientation`; swap width and height for
orientation values 5 through 8 so HTML dimensions match the auto-oriented
derivative. Return those logical dimensions and `info.GetPixelMemorySize()`.

- [ ] **Step 4: Write failing scanner tests**

Build a temporary tree containing numeric and unprefixed album directories,
dated and undated supported images, `notes.txt`, a corrupt `.jpg`, and a nested
image. Assert:

- only immediate valid supported images appear;
- albums use the approved numeric/alphabetic ordering;
- dated photos sort chronologically, filename breaks equal-date ties, and
  undated photos follow by filename;
- dimensions, length, last-write ticks, and normalized relative path are stored;
- the first photo is the album cover; and
- skipped corrupt, unsupported, nested, or changing files increment the scan's
  skipped-file count; and
- `RescanAlbumsAsync` replaces a changed album while retaining untouched albums.

- [ ] **Step 5: Run scanner tests and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter FullyQualifiedName~PhotoScannerTests`

Expected: FAIL because `PhotoScanner` is absent.

- [ ] **Step 6: Implement full and targeted scanning**

```csharp
public interface IPhotoScanner
{
    Task<PhotoScanResult> ScanAllAsync(CancellationToken cancellationToken);
    Task<PhotoScanResult> RescanAlbumsAsync(
        PortfolioSnapshot current,
        IReadOnlySet<string> albumDirectoryNames,
        CancellationToken cancellationToken);
}

public sealed record PhotoScanResult(PortfolioSnapshot Snapshot, int SkippedFileCount);
```

Use non-recursive enumeration and `FileAttributes.ReparsePoint` checks. Wrap
each file's `IdentifyAsync` independently in expected I/O/ImageSharp exception
handling, log a warning, and continue. Reject metadata whose estimated decoded
pixel size exceeds `512L * 1024 * 1024`. Read `FileInfo` length and last-write
ticks both before and after identification; skip a file that changes during the
read. Store only normalized relative paths, never absolute paths. Targeted
rescans replace or remove named albums, then rebuild the complete catalog so
ordering and collision slugs remain correct. Sort capture dates ascending;
break ties with ordinal-ignore-case filename and then ordinal filename. Put
undated photos after all dated photos using the same filename comparison.
Reject a targeted album name unless `Path.GetFileName(name) == name` and it is
neither `.` nor `..`; the scanner never accepts a rooted or multi-segment album
argument.

- [ ] **Step 7: Run scanner coverage and commit**

Run: `dotnet test FolderFolio.slnx --filter "FullyQualifiedName~MetadataReaderTests|FullyQualifiedName~PhotoScannerTests"`

Expected: PASS.

```bash
git add src/FolderFolio/Indexing tests/FolderFolio.Tests/Indexing tests/FolderFolio.Tests/Support
git commit -m "feat: scan filesystem albums and photo metadata"
```

---

### Task 4: Coordinate watcher events and background indexing

**Files:**
- Create: `src/FolderFolio/Indexing/IndexRefreshRequest.cs`
- Create: `src/FolderFolio/Indexing/IIndexRefreshQueue.cs`
- Create: `src/FolderFolio/Indexing/IndexRefreshQueue.cs`
- Create: `src/FolderFolio/Indexing/IndexRefreshCoordinator.cs`
- Create: `src/FolderFolio/Indexing/IPhotoRootWatcher.cs`
- Create: `src/FolderFolio/Indexing/PhotoRootEventMapper.cs`
- Create: `src/FolderFolio/Indexing/FileSystemPhotoRootWatcher.cs`
- Create: `src/FolderFolio/Indexing/IndexingService.cs`
- Create: `tests/FolderFolio.Tests/Support/StubPhotoScanner.cs`
- Create: `tests/FolderFolio.Tests/Indexing/IndexRefreshQueueTests.cs`
- Create: `tests/FolderFolio.Tests/Indexing/PhotoRootEventMapperTests.cs`
- Create: `tests/FolderFolio.Tests/Indexing/IndexRefreshCoordinatorTests.cs`
- Modify: `src/FolderFolio/Program.cs`

**Interfaces:**
- Consumes: `IPhotoScanner`, `IPortfolioIndex`, `TimeProvider`, and `FolderFolioOptions`.
- Produces: a singleton bounded refresh queue, watcher adapter, coordinator, and hosted startup/index loop.

- [ ] **Step 1: Write failing queue and event-mapping tests**

Assert a queue with capacity two accepts two album requests, escalates the third
to a full scan, and exposes that escalation even when the channel is full.
For root `/photos`, assert changes to `/photos/01-Landscapes/a.jpg` target
`01-Landscapes`, changes below `/photos/01-Landscapes/nested/` are ignored, and
top-level album add/remove/rename requests a full scan. A cross-album photo
rename returns both old and new album names.

- [ ] **Step 2: Run queue/mapper tests and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter "FullyQualifiedName~IndexRefreshQueueTests|FullyQualifiedName~PhotoRootEventMapperTests"`

Expected: FAIL because the queue and mapper are absent.

- [ ] **Step 3: Implement the bounded queue and pure mapper**

```csharp
public sealed record IndexRefreshRequest(bool FullScan, ImmutableHashSet<string> AlbumDirectoryNames)
{
    public static IndexRefreshRequest Full { get; } =
        new(true, ImmutableHashSet<string>.Empty);
    public static IndexRefreshRequest Album(string name) =>
        new(false, ImmutableHashSet.Create(StringComparer.Ordinal, name));
}

public interface IIndexRefreshQueue
{
    void RequestFullScan();
    void RequestAlbum(string albumDirectoryName);
    ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken);
    bool TryRead(out IndexRefreshRequest request);
    bool ConsumeForcedFullScan();
}
```

Use a bounded channel with `FullMode=Wait`, one reader, and multiple writers;
producers use only nonblocking `TryWrite`. If an album write returns false, set
an interlocked full-scan flag. `RequestFullScan` sets the same flag and also
attempts to enqueue a full request so an empty reader wakes; if the channel is
already full, its existing items provide that wake-up. `ConsumeForcedFullScan`
atomically reads and clears the flag. The mapper
canonicalizes paths, counts relative segments, and never returns a raw arbitrary
path to the scanner.

- [ ] **Step 4: Write failing debounce/coordinator tests**

Using `FakeTimeProvider`, enqueue the same album several times, start
`ProcessNextBatchAsync`, advance 750 milliseconds, and assert the stub scanner's
targeted method ran once with one album. Assert a full request wins over album
requests. Assert successful scans publish Ready, while an exception marks
Degraded and preserves the prior snapshot.

- [ ] **Step 5: Run coordinator tests and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter FullyQualifiedName~IndexRefreshCoordinatorTests`

Expected: FAIL because the coordinator is absent.

- [ ] **Step 6: Implement coordinator, watcher, and hosted lifecycle**

`IndexRefreshCoordinator.ProcessNextBatchAsync` waits for one event, drains the
queue, delays through repeated 750 ms quiet periods while new events arrive,
deduplicates album names with ordinal comparison, and calls either full or
targeted scanning. It measures duration with `TimeProvider.GetTimestamp()` and
publishes the returned snapshot. After every scan it logs scan kind, album
count, photo count, skipped-file count, and elapsed milliseconds.

`FileSystemPhotoRootWatcher` uses `IncludeSubdirectories=true` with directory
name, file name, size, and last-write notifications. Created/deleted/changed and
both sides of renamed events pass through `PhotoRootEventMapper`; `Error`
requests a full scan. It owns and disposes exactly one watcher.

`IndexingService` loops while the root is unavailable, calls
`MarkStarting("Photo root is unavailable.")`, and retries after five seconds.
Once available it starts the watcher before the initial full scan, publishes the
result, then repeatedly processes refresh batches. If that first scan fails,
log the exception, remain Starting with a sanitized public error, and retry;
only failures after a successful publication transition to Degraded. Register the index, scanner,
queue, coordinator, watcher, `TimeProvider.System`, and hosted service in DI.

- [ ] **Step 7: Run index lifecycle tests and commit**

Run: `dotnet test FolderFolio.slnx --filter "FullyQualifiedName~IndexRefresh|FullyQualifiedName~PhotoRootEventMapper"`

Expected: PASS.

```bash
git add src/FolderFolio/Indexing src/FolderFolio/Program.cs tests/FolderFolio.Tests/Indexing tests/FolderFolio.Tests/Support
git commit -m "feat: refresh index from debounced filesystem events"
```

---

### Task 5: Create versioned derivative identities and guard source paths

**Files:**
- Create: `src/FolderFolio/Imaging/DerivativeKind.cs`
- Create: `src/FolderFolio/Imaging/DerivativeIdentity.cs`
- Create: `src/FolderFolio/Imaging/IDerivativeKeyFactory.cs`
- Create: `src/FolderFolio/Imaging/DerivativeKeyFactory.cs`
- Create: `src/FolderFolio/Imaging/ISourcePathGuard.cs`
- Create: `src/FolderFolio/Imaging/SourcePathGuard.cs`
- Create: `tests/FolderFolio.Tests/Imaging/DerivativeKeyFactoryTests.cs`
- Create: `tests/FolderFolio.Tests/Imaging/SourcePathGuardTests.cs`

**Interfaces:**
- Consumes: `IndexedPhoto` fingerprints and validated image options.
- Produces: `IDerivativeKeyFactory.Create(IndexedPhoto, DerivativeKind)` and `ISourcePathGuard.TryResolve(IndexedPhoto, out string)`.

- [ ] **Step 1: Write failing identity tests**

Assert the same request is deterministic; changing relative path, byte length,
last-write ticks, derivative kind, long-edge setting, WebP quality, or cache
schema changes the key and URL version. Assert the ETag is quoted and the
version is a stable lowercase prefix of the full SHA-256 key.

- [ ] **Step 2: Run identity tests and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter FullyQualifiedName~DerivativeKeyFactoryTests`

Expected: FAIL because derivative identity types are absent.

- [ ] **Step 3: Implement derivative identity generation**

```csharp
public enum DerivativeKind { Grid, Web }

public sealed record DerivativeIdentity(
    string CacheKey,
    string Version,
    string ETag,
    int MaxLongEdge,
    int WebPQuality);

public interface IDerivativeKeyFactory
{
    DerivativeIdentity Create(IndexedPhoto photo, DerivativeKind kind);
}
```

Write an unindented canonical JSON object with fixed property order containing
cache schema `1`, normalized relative path, length, last-write ticks, lowercase
kind, selected dimension, quality, and `webp`; hash its UTF-8 bytes. This avoids
delimiter ambiguity for legal filenames. Use the full lowercase SHA-256 hex for
the key, the first 24 chars for the URL version, and the quoted full key for
ETag.

- [ ] **Step 4: Write failing containment and fingerprint tests**

Create a valid root/file and matching `IndexedPhoto`; resolution succeeds and
returns the canonical file. Assert false for `../outside.jpg`, an absolute
relative path, a symlink/reparse-point source, a missing file, changed length,
and changed last-write ticks.

- [ ] **Step 5: Run path-guard tests and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter FullyQualifiedName~SourcePathGuardTests`

Expected: FAIL because `SourcePathGuard` is absent.

- [ ] **Step 6: Implement trusted-root resolution**

Canonicalize `PhotoRoot` once with a trailing directory separator. Reject
rooted relative paths. Combine only the trusted root and the indexed relative
path, call `Path.GetFullPath`, compare with the canonical root using the
platform-appropriate case comparison, then inspect `FileInfo` attributes,
length, and UTC last-write ticks. Do not accept a route value in this API.

- [ ] **Step 7: Run imaging foundation tests and commit**

Run: `dotnet test FolderFolio.slnx --filter "FullyQualifiedName~DerivativeKeyFactoryTests|FullyQualifiedName~SourcePathGuardTests"`

Expected: PASS.

```bash
git add src/FolderFolio/Imaging tests/FolderFolio.Tests/Imaging
git commit -m "feat: secure and version derivative sources"
```

---

### Task 6: Generate privacy-safe ImageSharp WebP derivatives

**Files:**
- Create: `src/FolderFolio/Imaging/IImageDerivativeGenerator.cs`
- Create: `src/FolderFolio/Imaging/ImageSharpDerivativeGenerator.cs`
- Create: `tests/FolderFolio.Tests/Imaging/ImageSharpDerivativeGeneratorTests.cs`
- Modify: `tests/FolderFolio.Tests/Support/ImageFixtureFactory.cs`

**Interfaces:**
- Produces: `IImageDerivativeGenerator.WriteWebPAsync(string, Stream, int, int, CancellationToken)`.

- [ ] **Step 1: Write failing derivative output tests**

Create a `40x80` JPEG with EXIF orientation `Rotate90`. Generate with a 50-pixel
bound and assert the WebP identifies as `50x25`, its EXIF profile is null, and
its format is WebP. Generate a `20x10` source at a 400-pixel bound and assert it
remains `20x10`. Add EXIF GPS, IPTC, and XMP fixture metadata and assert none is
present in the output.

- [ ] **Step 2: Run generator tests and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter FullyQualifiedName~ImageSharpDerivativeGeneratorTests`

Expected: FAIL because the generator is absent.

- [ ] **Step 3: Implement bounded decode, orientation, resize, and encoding**

Use:

```csharp
var decoderOptions = new DecoderOptions
{
    MaxFrames = 1,
    SkipMetadata = false,
    SegmentIntegrityHandling = SegmentIntegrityHandling.Strict,
    TargetSize = new Size(maxLongEdge, maxLongEdge)
};
using var image = await Image.LoadAsync(decoderOptions, sourcePath, cancellationToken);
image.Mutate(context => context
    .AutoOrient()
    .Resize(new ResizeOptions
    {
        Size = new Size(maxLongEdge, maxLongEdge),
        Mode = ResizeMode.Max
    }));
var encoder = new WebpEncoder
{
    FileFormat = WebpFileFormatType.Lossy,
    Quality = quality,
    SkipMetadata = true
};
await image.SaveAsWebpAsync(destination, encoder, cancellationToken);
```

Null EXIF, IPTC, and XMP profiles after `AutoOrient` as defense in depth. Leave
ICC color handling intact; the encoder metadata switch controls emitted private
metadata.

- [ ] **Step 4: Run generator tests and commit**

Run: `dotnet test FolderFolio.slnx --filter FullyQualifiedName~ImageSharpDerivativeGeneratorTests`

Expected: PASS.

```bash
git add src/FolderFolio/Imaging tests/FolderFolio.Tests/Imaging tests/FolderFolio.Tests/Support/ImageFixtureFactory.cs
git commit -m "feat: generate metadata-free WebP derivatives"
```

---

### Task 7: Add atomic disk caching and single-flight generation

**Files:**
- Create: `src/FolderFolio/Imaging/CachedDerivative.cs`
- Create: `src/FolderFolio/Imaging/StaleSourceException.cs`
- Create: `src/FolderFolio/Imaging/IDerivativeService.cs`
- Create: `src/FolderFolio/Imaging/DerivativeService.cs`
- Create: `tests/FolderFolio.Tests/Support/CountingDerivativeGenerator.cs`
- Create: `tests/FolderFolio.Tests/Support/TestHostApplicationLifetime.cs`
- Create: `tests/FolderFolio.Tests/Imaging/DerivativeServiceTests.cs`
- Modify: `src/FolderFolio/Program.cs`

**Interfaces:**
- Consumes: key factory, source path guard, image generator, cache root, and host shutdown token.
- Produces: `IDerivativeService.GetOrCreateAsync(IndexedPhoto, DerivativeKind, CancellationToken)`.

- [ ] **Step 1: Write failing cache and concurrency tests**

Use a counting generator held on a `TaskCompletionSource`. Start 20 simultaneous
requests for one identity, release generation, and assert all results share one
path while the generator count is exactly one. While held, assert the final
`.webp` does not exist; after release, assert it exists, no `.tmp` files remain,
and a later request is a cache hit. Assert a failed generation leaves neither a
final nor temporary file. Assert a fingerprint mismatch throws
`StaleSourceException` without invoking the generator.

- [ ] **Step 2: Run service tests and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter FullyQualifiedName~DerivativeServiceTests`

Expected: FAIL because the derivative service is absent.

- [ ] **Step 3: Implement the single-flight atomic cache**

```csharp
public sealed record CachedDerivative(
    string AbsolutePath,
    long Length,
    DateTimeOffset LastModifiedUtc);

public interface IDerivativeService
{
    Task<CachedDerivative> GetOrCreateAsync(
        IndexedPhoto photo,
        DerivativeKind kind,
        CancellationToken cancellationToken);
}
```

Store files as `{CacheRoot}/{firstTwoKeyChars}/{fullKey}.webp`. Check for a
completed file before entering a
`ConcurrentDictionary<string, Lazy<Task<CachedDerivative>>>`, then check again
inside the selected lazy task. Resolve and validate the source before and after
generation. Write a GUID-named `.tmp` in the same destination directory, flush
and close it, then use `File.Move(temp, final, overwrite: false)`. Delete the
temporary file in `finally`. Remove the exact lazy instance from the dictionary
after success or failure.

The shared generator uses `IHostApplicationLifetime.ApplicationStopping`;
individual callers await it with `WaitAsync(cancellationToken)` so one cancelled
HTTP request does not cancel work shared by other callers.

- [ ] **Step 4: Register imaging services and run tests**

Register the key factory, path guard, image generator, and derivative service as
singletons. Create the cache root lazily on first write.

Run: `dotnet test FolderFolio.slnx --filter FullyQualifiedName~DerivativeServiceTests`

Expected: PASS.

- [ ] **Step 5: Commit the derivative cache**

```bash
git add src/FolderFolio/Imaging src/FolderFolio/Program.cs tests/FolderFolio.Tests/Imaging tests/FolderFolio.Tests/Support
git commit -m "feat: cache derivatives with single-flight generation"
```

---

### Task 8: Serve indexed media with immutable HTTP caching

**Files:**
- Create: `src/FolderFolio/Web/IMediaUrlBuilder.cs`
- Create: `src/FolderFolio/Web/MediaUrlBuilder.cs`
- Create: `src/FolderFolio/Web/MediaEndpoint.cs`
- Create: `tests/FolderFolio.Tests/Support/FolderFolioWebApplicationFactory.cs`
- Create: `tests/FolderFolio.Tests/Support/StubDerivativeService.cs`
- Create: `tests/FolderFolio.Tests/Web/MediaEndpointTests.cs`
- Create: `tests/FolderFolio.Tests/Web/MediaUrlBuilderTests.cs`
- Modify: `src/FolderFolio/Program.cs`

**Interfaces:**
- Consumes: immutable index, key factory, derivative service, and refresh queue.
- Produces: named route `folderfolio-media` and `IMediaUrlBuilder.Build(IndexedAlbum, IndexedPhoto, DerivativeKind)`.

- [ ] **Step 1: Write failing URL and endpoint tests**

Seed a test index with one album/photo and use the real key factory. Assert the
URL builder emits an encoded path in this form:

```text
/media/landscapes/{opaquePhotoId}/grid?v={currentVersion}
```

Request the URL and assert status `200`, `Content-Type: image/webp`, the quoted
ETag, and `Cache-Control: public,max-age=31536000,immutable`. Assert `404` for an
unknown album, photo, size, absent version, or stale version. Assert responses
do not contain the configured photo-root path.

- [ ] **Step 2: Run media tests and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter "FullyQualifiedName~MediaEndpointTests|FullyQualifiedName~MediaUrlBuilderTests"`

Expected: FAIL because media routing is absent.

- [ ] **Step 3: Implement route-generated URLs and lookup-only delivery**

`MediaUrlBuilder` uses `LinkGenerator.GetPathByName` with route values and
`QueryHelpers.AddQueryString` for the version; it does not interpolate paths.
Map this endpoint and name it `folderfolio-media`:

```csharp
app.MapGet(
    "/media/{albumSlug}/{photoId}/{size}",
    MediaEndpoint.HandleAsync)
   .WithName(MediaEndpoint.RouteName);
```

The handler captures `IPortfolioIndex.Current` once, looks up album and photo,
parses only `grid` or `web`, recomputes the expected identity, compares `v` with
fixed ordinal semantics, and calls the derivative service. Before returning
`Results.PhysicalFile`, set immutable `Cache-Control`, ETag, and last-modified
headers. Catch `StaleSourceException`, enqueue the indexed album directory for
refresh, and return `404`. Do not accept or log a source path from the route.

- [ ] **Step 4: Run media tests and commit**

Run: `dotnet test FolderFolio.slnx --filter "FullyQualifiedName~MediaEndpointTests|FullyQualifiedName~MediaUrlBuilderTests"`

Expected: PASS.

```bash
git add src/FolderFolio/Web src/FolderFolio/Program.cs tests/FolderFolio.Tests/Web tests/FolderFolio.Tests/Support
git commit -m "feat: serve versioned indexed image derivatives"
```

---

### Task 9: Render the album index and gallery Razor Pages

**Files:**
- Create: `src/FolderFolio/Web/ViewModels/AlbumCardViewModel.cs`
- Create: `src/FolderFolio/Web/ViewModels/PhotoViewModel.cs`
- Create: `src/FolderFolio/Web/ViewModels/AlbumGalleryViewModel.cs`
- Create: `src/FolderFolio/Web/ViewModels/IndexPageState.cs`
- Create: `src/FolderFolio/Web/ViewModels/PortfolioViewModelFactory.cs`
- Create: `src/FolderFolio/Pages/_ViewImports.cshtml`
- Create: `src/FolderFolio/Pages/_ViewStart.cshtml`
- Create: `src/FolderFolio/Pages/Shared/_Layout.cshtml`
- Create: `src/FolderFolio/Pages/Index.cshtml`
- Create: `src/FolderFolio/Pages/Index.cshtml.cs`
- Create: `src/FolderFolio/Pages/Albums/Details.cshtml`
- Create: `src/FolderFolio/Pages/Albums/Details.cshtml.cs`
- Create: `src/FolderFolio/Pages/Error.cshtml`
- Create: `src/FolderFolio/Pages/Error.cshtml.cs`
- Create: `tests/FolderFolio.Tests/Web/IndexPageTests.cs`
- Create: `tests/FolderFolio.Tests/Web/AlbumDetailsPageTests.cs`
- Modify: `src/FolderFolio/Program.cs`

**Interfaces:**
- Consumes: index publication, site options, and media URL builder.
- Produces: `/` and `/albums/{slug}` with presentation-only immutable view models.

- [ ] **Step 1: Write failing PageModel and rendered-page tests**

Assert `IndexModel` maps site title/tagline and snapshot order, distinguishes
Preparing, Empty, and Populated states, and never exposes a source relative
path. Assert `DetailsModel.OnGet("landscapes")` maps photo positions, dimensions,
generic accessible labels, grid URL, and web URL. Unknown slugs return
`NotFoundResult` after a successful scan; a Starting index renders the Preparing
state instead.

Using the web factory, assert `/` contains album title/count/cover URL and
`/albums/landscapes` contains one `data-lightbox-trigger` per photo with
non-empty `data-web-src`, `data-alt`, integer `data-index`, and image width and
height. Each tile also contains a hidden, labelled `Image unavailable`
fallback. Avoid whole-page snapshots and CSS-class-count assertions.

- [ ] **Step 2: Run page tests and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter "FullyQualifiedName~IndexPageTests|FullyQualifiedName~AlbumDetailsPageTests"`

Expected: FAIL because page models and views are absent.

- [ ] **Step 3: Implement view models and factory**

Use these public shapes:

```csharp
public sealed record AlbumCardViewModel(
    string Slug, string Title, int PhotoCount,
    string CoverUrl, int CoverWidth, int CoverHeight);

public sealed record PhotoViewModel(
    string Id, string AccessibleName, string GridUrl, string WebUrl,
    int Width, int Height, int Position, bool LoadEagerly);

public sealed record AlbumGalleryViewModel(
    string Slug, string Title, int PhotoCount,
    IReadOnlyList<PhotoViewModel> Photos);

public enum IndexPageState { Preparing, Empty, Populated }
```

The factory uses current media URLs and labels photos as
`Photo {position} of {count} in {albumTitle}`. Mark the first four photos eager.

- [ ] **Step 4: Implement the Razor routes and explicit states**

`IndexModel` exposes `SiteTitle`, `Tagline`, `IndexPageState`, and album cards.
`DetailsModel` exposes `IsPreparing` and one gallery. Put
`@page "/albums/{slug}"` on the album view. Home album covers use a descriptive
alt string; gallery images use empty alt text because the containing button has
the full accessible label. Eager images use `loading="eager"` and
`fetchpriority="high"`; all others use `loading="lazy"`. Every image emits
intrinsic dimensions and `decoding="async"`.
Treat any publication with a non-null last-success time as readable, including
Degraded publications; only a publication with no successful snapshot renders
Preparing.

- [ ] **Step 5: Run page tests and commit**

Run: `dotnet test FolderFolio.slnx --filter "FullyQualifiedName~IndexPageTests|FullyQualifiedName~AlbumDetailsPageTests"`

Expected: PASS.

```bash
git add src/FolderFolio/Pages src/FolderFolio/Web/ViewModels src/FolderFolio/Program.cs tests/FolderFolio.Tests/Web
git commit -m "feat: render portfolio and album pages"
```

---

### Task 10: Add the responsive visual system and accessible lightbox

**Files:**
- Create: `src/FolderFolio/Pages/Shared/_PhotoLightbox.cshtml`
- Create: `src/FolderFolio/wwwroot/css/site.css`
- Create: `src/FolderFolio/wwwroot/js/lightbox.js`
- Create: `tests/FolderFolio.Tests/Web/LightboxMarkupTests.cs`
- Modify: `src/FolderFolio/Pages/Albums/Details.cshtml`
- Modify: `src/FolderFolio/Pages/Shared/_Layout.cshtml`

**Interfaces:**
- Consumes: the stable gallery `data-*` contract from Task 9.
- Produces: `window.FolderFolioLightbox.init(root = document)` and native-dialog controls.

- [ ] **Step 1: Write the failing lightbox markup contract test**

Request a populated album and assert exactly one `dialog#photo-lightbox` plus
controls carrying `data-lightbox-close`, `data-lightbox-previous`, and
`data-lightbox-next`. Assert all three controls have accessible labels, the
dialog's `aria-labelledby="lightbox-title"` resolves to the caption with that
ID, `img[data-lightbox-image]` is present, the page loads `/js/lightbox.js`, and
the dialog starts with `data-state="idle"`.

- [ ] **Step 2: Run the markup test and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter FullyQualifiedName~LightboxMarkupTests`

Expected: FAIL because the partial and script are absent.

- [ ] **Step 3: Implement the dialog partial and JavaScript state machine**

The partial contains one `<dialog>`, close/previous/next buttons, one image, one
caption with ID `lightbox-title`, and an error/retry region. The dialog's
`aria-labelledby` points to that exact caption. `lightbox.js` exports one global
initializer and keeps `triggers`, `activeIndex`, and `activeTrigger` in its
closure. It must:

- open through `showModal()`, save the trigger, focus Close, and set loading;
- copy only the selected trigger's server-rendered `data-web-src` and `data-alt`,
  updating the displayed image alt and caption together;
- set Ready on image load and Error on image failure while keeping Close usable;
- navigate within array bounds and update disabled states;
- handle Left, Right, and Escape without overriding native Tab focus trapping;
- close on a true backdrop click but not an image click;
- clear the image source on close and restore focus to `activeTrigger`; and
- make retry reassign the canonical URL with a client-only `retry` query value
  after clearing the old source; this value must not replace or alter `v`.

Also attach an error listener to each grid image. On failure it marks the
containing `data-photo-tile` as `data-state="error"`, hides the broken image,
and exposes that tile's `Image unavailable` fallback without disabling the
lightbox trigger.

Call `FolderFolioLightbox.init()` on `DOMContentLoaded` and make repeated calls
idempotent with a root-level data flag.

- [ ] **Step 4: Implement the approved responsive styling**

Define CSS custom properties for the near-black canvas, warm text, muted text,
border, and spacing. Use an album grid with
`repeat(auto-fit, minmax(min(100%, 17rem), 1fr))` and a gallery grid with
`repeat(auto-fill, minmax(min(100%, 13rem), 1fr))`. Album covers use a
consistent `3 / 2` center crop; gallery images use intrinsic ratio and
`object-fit: contain`. Controls have at least 44 px hit targets, dialog content
respects safe-area insets, and `prefers-reduced-motion: reduce` disables
transitions. Provide Preparing, Empty, unavailable-image, focus-visible, and
error styles without animation-heavy effects.

The supported frontend baseline is the current stable releases of Chrome,
Edge, Firefox, and Safari, all of which provide native `<dialog>`. Browser
automation remains outside the approved MVP; Task 12 performs the interaction
and responsive checks manually.

- [ ] **Step 5: Run markup and page tests, then commit**

Run: `dotnet test FolderFolio.slnx --filter "FullyQualifiedName~LightboxMarkupTests|FullyQualifiedName~PageTests"`

Expected: PASS.

```bash
git add src/FolderFolio/Pages src/FolderFolio/wwwroot tests/FolderFolio.Tests/Web
git commit -m "feat: add responsive accessible photo lightbox"
```

---

### Task 11: Add health reporting, proxy handling, and the final HTTP pipeline

**Files:**
- Create: `src/FolderFolio/Web/HealthEndpoint.cs`
- Create: `src/FolderFolio/Web/ForwardedHeadersSetup.cs`
- Create: `tests/FolderFolio.Tests/Web/HealthEndpointTests.cs`
- Create: `tests/FolderFolio.Tests/Web/ForwardedHeadersSetupTests.cs`
- Modify: `src/FolderFolio/Program.cs`

**Interfaces:**
- Consumes: index publication.
- Produces: `GET /health`, one-hop `X-Forwarded-For`/`X-Forwarded-Proto` handling, and the production middleware order.

- [ ] **Step 1: Write failing health and proxy-option tests**

Assert `/health` returns `503` and `Cache-Control: no-store` while Starting;
`200` with status, generation, album count, photo count, and last-success time
while Ready; and `503` while Degraded without exposing filesystem paths or an
exception stack. Assert forwarded options enable only `XForwardedFor` and
`XForwardedProto`, set `ForwardLimit=1`, and clear known proxies/networks for the
documented loopback-bound host-tunnel deployment.

- [ ] **Step 2: Run health/proxy tests and confirm failure**

Run: `dotnet test FolderFolio.slnx --filter "FullyQualifiedName~HealthEndpointTests|FullyQualifiedName~ForwardedHeadersSetupTests"`

Expected: FAIL because the endpoint and setup object are absent.

- [ ] **Step 3: Implement health and forwarded-header setup**

`HealthEndpoint.Handle(HttpContext, IPortfolioIndex)` captures one publication,
sets `Cache-Control: no-store`, and returns a camel-case JSON record with only
the approved public fields. Ready maps to `200`; Starting and Degraded map to
`503`.

`ForwardedHeadersSetup.Configure(ForwardedHeadersOptions options)` sets the two
flags, a one-hop limit, and clears known lists. Add a comment tying this trust
choice to Compose's `127.0.0.1` binding and directing non-loopback deployments
to configure explicit known proxy addresses/networks.

- [ ] **Step 4: Finalize middleware and endpoint ordering**

The final order is:

```csharp
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseStaticFiles();
app.UseRouting();
app.MapGet("/health", HealthEndpoint.Handle);
MediaEndpoint.Map(app);
app.MapRazorPages();
app.Run();
```

Do not add `X-Forwarded-Host`, authentication middleware, or a public diagnostic
endpoint.

- [ ] **Step 5: Run the complete suite and commit**

Run: `dotnet test FolderFolio.slnx`

Expected: all tests pass with zero warnings.

```bash
git add src/FolderFolio/Program.cs src/FolderFolio/Web tests/FolderFolio.Tests/Web
git commit -m "feat: report index health behind one trusted proxy hop"
```

---

### Task 12: Package, document, and smoke-test the MVP

**Files:**
- Create: `Dockerfile`
- Create: `docker-compose.yml`
- Create: `.dockerignore`
- Modify: `.gitignore`
- Replace: `README.md`

**Interfaces:**
- Consumes: the completed web application and configuration contract.
- Produces: a non-root Linux image, loopback-bound Compose service, persistent derivative volume, and operator documentation.

- [ ] **Step 1: Create the multi-stage non-root Dockerfile**

Use .NET 10 SDK/runtime images. The complete build-stage sequence is:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Packages.props ./
COPY src/FolderFolio/FolderFolio.csproj src/FolderFolio/packages.lock.json src/FolderFolio/
RUN dotnet restore src/FolderFolio/FolderFolio.csproj --locked-mode
COPY src/FolderFolio/ src/FolderFolio/
RUN dotnet publish src/FolderFolio/FolderFolio.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false
```

Then copy to the runtime image. In the runtime stage:

```dockerfile
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
RUN mkdir -p /cache && chown -R app:app /cache
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "FolderFolio.dll"]
```

Do not add a curl-based Docker healthcheck because the runtime image does not
ship curl.

- [ ] **Step 2: Create Compose and ignore rules**

Compose builds the repository Dockerfile, restarts unless stopped, binds only
`127.0.0.1:8080:8080`, supplies Production plus photo/cache option overrides,
mounts `./photos:/photos:ro`, and mounts a named `folderfolio-cache:/cache`
volume. Set `image: folderfolio:local` so verification does not depend on the
checkout directory name. Add build outputs, test results, `.git`, local `photos`, and local cache
content to `.dockerignore`; add `/photos/*` and `/cache/` to `.gitignore` while
allowing an optional `/photos/.gitkeep`.

Use this Compose shape:

```yaml
services:
  folderfolio:
    image: folderfolio:local
    build:
      context: .
      dockerfile: Dockerfile
    restart: unless-stopped
    ports:
      - "127.0.0.1:8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      FolderFolio__PhotoRoot: /photos
      FolderFolio__CacheRoot: /cache
    volumes:
      - ./photos:/photos:ro
      - folderfolio-cache:/cache

volumes:
  folderfolio-cache: {}
```

- [ ] **Step 3: Replace the README with operator documentation**

Document prerequisites, `mkdir -p photos/01-Landscapes`, supported extensions,
prefix/title/slug rules, EXIF ordering, `docker compose up -d --build`, the
loopback Cloudflare Tunnel origin `http://localhost:8080`, every environment
variable, `/health` semantics, read-only photos and named cache volume,
non-root permissions for alternate bind mounts, cache invalidation/cleanup,
logs, local `dotnet run`, and every deliberate MVP omission from the spec.
Include the ImageSharp split-license review note without offering legal advice.

- [ ] **Step 4: Run repository verification**

Run:

```bash
dotnet restore FolderFolio.slnx --locked-mode
dotnet test FolderFolio.slnx --no-restore
dotnet publish src/FolderFolio/FolderFolio.csproj -c Release --no-restore -o artifacts/publish
dotnet format FolderFolio.slnx --verify-no-changes --no-restore
docker compose config
docker compose build
```

Expected: restore, tests, publish, formatting check, Compose validation, and
Linux image build all succeed. If formatting fails, run `dotnet format
FolderFolio.slnx --no-restore`, inspect the mechanical changes, and rerun the
verification commands.

- [ ] **Step 5: Perform the live smoke check**

Place one real supported photo in `photos/01-Test-Album/`, start Compose, and
poll `http://127.0.0.1:8080/health` until it returns `200`. Confirm `/` shows the
album and `/albums/test-album` shows the photo. Open the photo and verify Close,
Left, Right, and Escape, focus restoration, and responsive layouts near 375,
768, and 1440 CSS pixels. Replace the source at the same filename, wait for the
watcher quiet period, and reload the album page until its rendered media URL
version changes; fetch that new URL and confirm it succeeds. Add and remove a
second photo and confirm the count updates after debounce. Download one media
response and confirm it is WebP; metadata stripping remains covered by the
deterministic ImageSharp generator test rather than by Docker volume access.

Confirm the final pipeline still serves `/`, `/albums/test-album`, a current
`/media/...` URL, `/health`, `/css/site.css`, and `/js/lightbox.js`. Verify the
default named cache is writable and photos are read-only. For an alternate
Linux cache bind mount, document and verify that its directory is writable by
the image's `app` user (UID 1654); never solve permission errors by running the
service as root.

Run:

```bash
docker compose logs --no-color folderfolio
docker compose down
```

Expected: scan summaries contain counts/duration without public path leakage;
shutdown is clean and the named derivative cache remains.

- [ ] **Step 6: Commit deployment and documentation**

```bash
git add Dockerfile docker-compose.yml .dockerignore .gitignore README.md
git commit -m "docs: package and document FolderFolio MVP"
```

- [ ] **Step 7: Record final evidence**

Run `git status --short`, `git log --oneline -12`, `dotnet test
FolderFolio.slnx --no-restore`, and `docker image inspect folderfolio:local
--format '{{.Config.User}}'`.

Expected: worktree clean, task commits present, tests pass, and the container
user is `app`.
