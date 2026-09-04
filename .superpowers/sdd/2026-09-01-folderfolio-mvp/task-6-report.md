# Task 6 report: privacy-safe ImageSharp WebP derivatives

## Implementation

- Added `IImageDerivativeGenerator` with the Task 6 `WriteWebPAsync` contract.
- Added `ImageSharpDerivativeGenerator`, which loads a single frame, auto-orients pixels, bounds the long edge, encodes lossy WebP at the requested quality, and clears EXIF, IPTC, and XMP before encoding with `SkipMetadata = true`.
- Bounded decode is applied only where the source exceeds the requested bound. A controlled fixture test established that ImageSharp 3.1.12's `DecoderOptions.TargetSize` enlarges a smaller source; leaving it enabled unconditionally produced `400x200` from a `20x10` source with a `400` bound. The conditional decode plus conditional resize preserves the no-enlargement contract.
- Expanded `ImageFixtureFactory` to write GPS EXIF, IPTC, and XMP metadata. The derivative test first reads the JPEG fixture and confirms that all three source profiles were encoded.

Files changed:

- `src/FolderFolio/Imaging/IImageDerivativeGenerator.cs`
- `src/FolderFolio/Imaging/ImageSharpDerivativeGenerator.cs`
- `tests/FolderFolio.Tests/Imaging/ImageSharpDerivativeGeneratorTests.cs`
- `tests/FolderFolio.Tests/Support/ImageFixtureFactory.cs`

## TDD evidence

Before writing tests, read the Superpowers TDD skill and its `writing-good-tests.md` reference. Tests use real JPEG/WebP files and ImageSharp decoding; no mocks.

### RED

Command:

```powershell
dotnet test FolderFolio.slnx --filter FullyQualifiedName~ImageSharpDerivativeGeneratorTests
```

Result: failed as expected because `ImageSharpDerivativeGenerator` did not exist (`CS0246` at both test instantiations).

After the first minimal implementation, the no-enlargement behavior test failed as expected:

```text
Expected: 20
Actual:   400
```

This exposed unconditional decode targeting as an ImageSharp 3.1.12 enlargement behavior.

### GREEN

Command:

```powershell
dotnet test FolderFolio.slnx --filter FullyQualifiedName~ImageSharpDerivativeGeneratorTests
```

Result: passed, 2 total / 2 succeeded / 0 failed. It verifies `40x80` JPEG + orientation 6 becomes `50x25` WebP, with no EXIF/IPTC/XMP, and that `20x10` remains `20x10` at a `400` bound.

## Verification

Focused suite:

```text
PASS: 2 total, 2 succeeded, 0 failed, 0 skipped
```

Full suite command:

```powershell
dotnet test FolderFolio.slnx
```

Result: 61 succeeded, 1 skipped, 1 failed. The failure is pre-existing/environmental in `PhotoScannerTests.ScanAllAsync_indexes_only_immediate_supported_files_in_album_and_photo_order`: Windows refused `File.CreateSymbolicLink` with "A required privilege is not held by the client." The existing `SourcePathGuardTests.TryResolve_rejects_a_symlink_source` was skipped for the same known Windows symlink privilege limitation. No Task 6 test failed.

`git diff --check` completed successfully (only line-ending warnings, no whitespace errors).

## Self-review

- Orientation is baked before output and the logical long edge is bounded without enlargement.
- Tests observe encoded output dimensions, format, and decoded metadata profiles, rather than mocks or implementation details.
- Metadata is cleared in-memory and the encoder independently suppresses metadata; ICC metadata is not explicitly cleared.
- No cache-concurrency, HTTP, source-guard, or DI behavior was added; guarded/trusted source resolution remains the responsibility of the later caller task.

## Concern

The supplied `SegmentIntegrityHandling.Strict` initializer cannot be used with the locked SixLabors.ImageSharp 3.1.12 dependency: its installed `DecoderOptions` has no `SegmentIntegrityHandling` member (confirmed in the package API XML and by compiler error CS0117). The implementation retains the supported decoder hardening available here (`MaxFrames = 1` and bounded decode); adding that exact strict-segment setting requires a dependency/API change outside Task 6 scope.

## Review fix round 1: guarded indexed inputs

### Root-cause trace

The original public `IImageDerivativeGenerator.WriteWebPAsync` accepted an arbitrary `string sourcePath`. Repository-wide call-site tracing found no generator consumers and no call from that boundary to `ISourcePathGuard.TryResolve`. In contrast, the Task 5 `ISourcePathGuard` contract accepts an `IndexedPhoto`, validates its relative path, canonicalizes it under the configured root, rejects reparse points, and compares the indexed length and timestamp to the current file. Therefore any caller could bypass Task 5 simply by passing a raw path directly to the generator.

The fix changes the generator boundary to `WriteWebPAsync(IndexedPhoto, Stream, int, int, CancellationToken)` and requires an `ISourcePathGuard` in the generator constructor. It calls `TryResolve` before identifying or decoding. A rejected source throws `InvalidOperationException` with no bytes written. `Image.IdentifyAsync` now receives supported ImageSharp 3.1.12 decoder options (`MaxFrames = 1`, `SkipMetadata = false`) before the size-dependent bounded decode options are constructed; `TargetSize` cannot be shared before dimensions are known without reintroducing enlargement.

### RED

The updated tests use real temporary JPEG/text files, a real `SourcePathGuard`, `FolderFolioOptions`, and `IndexedPhoto` fingerprints—no mocks.

Command:

```powershell
dotnet test FolderFolio.slnx --filter FullyQualifiedName~ImageSharpDerivativeGeneratorTests
```

Result: failed as expected against the raw-path public API. All four calls reported `CS1503` (`IndexedPhoto` cannot convert to `string`), and construction with the Task 5 guard reported `CS1729` (no matching constructor). The tests name the production breaks: bypassing source validation and allowing malformed guarded inputs to leave usable output.

### GREEN

Focused command:

```powershell
dotnet test FolderFolio.slnx --filter FullyQualifiedName~ImageSharpDerivativeGeneratorTests
```

Result: passed, 4 total / 4 succeeded / 0 failed. This includes the existing orientation, resize, format, and metadata assertions; a changed-on-disk indexed JPEG is rejected by the guard before the destination receives bytes; and a trusted malformed text file produces `UnknownImageFormatException` with a zero-byte destination.

Full-suite command:

```powershell
dotnet test FolderFolio.slnx
```

Result: 63 succeeded, 1 skipped, 1 failed. The only failure remains the environment-dependent `PhotoScannerTests.ScanAllAsync_indexes_only_immediate_supported_files_in_album_and_photo_order` symlink-creation failure on Windows (`A required privilege is not held by the client`); `SourcePathGuardTests.TryResolve_rejects_a_symlink_source` remains skipped for the same reason. No derivative-generator test failed.

### Review self-check

- The public generator method cannot now accept a raw filesystem path; every invocation must supply an `IndexedPhoto`.
- Removing the guard call makes the changed-fingerprint rejection test fail; allowing decode before guard risks violating the zero-byte assertion.
- Removing the metadata cleanup, orientation, or no-enlargement condition makes the established output tests fail.
- `git diff --check` passed before commit (only platform line-ending warnings were emitted).
