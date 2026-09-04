# Task 5 report: versioned derivative identities and trusted source paths

## Implementation

- Added `DerivativeKind`, `DerivativeIdentity`, and `IDerivativeKeyFactory`.
- Added `DerivativeKeyFactory`, which writes a compact canonical JSON object in fixed property order and hashes its UTF-8 bytes with SHA-256. The identity includes schema, normalized source-relative path, byte length, last-write UTC ticks, derivative kind, selected long edge, WebP quality, and `webp` format. It returns the complete lowercase hex key, a 24-character lowercase version, and a quoted ETag.
- Added `ISourcePathGuard` and `SourcePathGuard`. The guard canonicalizes the configured root once, rejects rooted or escaping relative paths, rejects reparse points, and verifies current length and UTC-write ticks before returning the resolved path.

## Files changed

- `src/FolderFolio/Imaging/DerivativeKind.cs`
- `src/FolderFolio/Imaging/DerivativeIdentity.cs`
- `src/FolderFolio/Imaging/IDerivativeKeyFactory.cs`
- `src/FolderFolio/Imaging/DerivativeKeyFactory.cs`
- `src/FolderFolio/Imaging/ISourcePathGuard.cs`
- `src/FolderFolio/Imaging/SourcePathGuard.cs`
- `tests/FolderFolio.Tests/Imaging/DerivativeKeyFactoryTests.cs`
- `tests/FolderFolio.Tests/Imaging/SourcePathGuardTests.cs`

## TDD evidence

### RED

`dotnet test FolderFolio.slnx --filter FullyQualifiedName~DerivativeKeyFactoryTests` failed before implementation with `CS0234`: `FolderFolio.Imaging` did not exist, plus missing `DerivativeKeyFactory`. The new source-path test was in the same test project and likewise could not resolve the absent imaging namespace.

### GREEN

The focused identity command subsequently passed: 2 succeeded, 0 failed.

The final focused command was:

```text
dotnet test FolderFolio.slnx --filter "FullyQualifiedName~DerivativeKeyFactoryTests|FullyQualifiedName~SourcePathGuardTests"
```

It passed with 7 total tests: 6 succeeded, 0 failed, and 1 skipped. The skipped test is the real symlink/reparse-point case: Windows returned `IOException: A required privilege is not held by the client` while creating the fixture. On systems permitted to create symlinks, that test invokes the real guard and asserts rejection.

## Full-suite verification

The full command `dotnet test FolderFolio.slnx` was run. It failed before Task 5 assertions because the pre-existing `PhotoScannerTests.ScanAllAsync_indexes_only_immediate_supported_files_in_album_and_photo_order` cannot create its symlink fixture under this Windows account, with the same privilege `IOException`. Result: 60 total, 58 succeeded, 1 failed, 1 skipped. No production or existing scanner test was changed to conceal this environment limitation.

A later fresh combined verification attempt encountered `NU1301` for `https://api.nuget.org/v3/index.json` (socket access forbidden) before it could run tests. The final focused command above completed successfully using the normal project invocation.

## Self-review and concerns

- Identity tests exercise deterministic output, source/settings mutation invalidation, lowercase key/version format, and ETag quoting using actual factory behavior.
- Path tests use real temporary files and verify trusted containment, rooted/traversal rejection, missing files, and length/timestamp drift. Symlink rejection is implemented and capability-tested where the Windows account permits creating a reparse point.
- The source guard accepts only `IndexedPhoto` instances; route inputs are never accepted as filesystem paths.
- Concern: the runner cannot create symlinks, so the symlink test is skipped locally; run the focused suite under an account with Developer Mode or symlink privilege for that assertion.

## Review fix round 1: filesystem-root photo roots

### Root cause and data flow

`SourcePathGuard` first canonicalized `PhotoRoot` with `Path.GetFullPath`, then called `Path.TrimEndingDirectorySeparator` and unconditionally appended a directory separator. For a volume root such as `C:\`, trimming correctly preserves the root separator; appending another produced `C:\\`. `Path.Combine` and `Path.GetFullPath` reduce the candidate to `C:\...`, which then fails the containment prefix check against the doubled-root value. Consequently all otherwise matching files under a configured volume-root photo directory were rejected.

### RED

Added `TryResolve_accepts_a_matching_photo_when_the_trusted_root_is_a_filesystem_root`. It creates a real temporary file, derives its relative path from `Path.GetPathRoot`, and invokes `SourcePathGuard` with that filesystem root.

```text
dotnet test FolderFolio.slnx --filter FullyQualifiedName~TryResolve_accepts_a_matching_photo_when_the_trusted_root_is_a_filesystem_root
```

Before the implementation change, the result was 1 total, 1 failed: `Assert.True() Failure; Expected: True; Actual: False`.

### GREEN

Changed the root construction to append a directory separator only when `Path.EndsInDirectorySeparator` is false. This preserves a filesystem root as-is and retains a delimiter for ordinary directory roots.

```text
dotnet test FolderFolio.slnx --filter "FullyQualifiedName~DerivativeKeyFactoryTests|FullyQualifiedName~SourcePathGuardTests"
```

Result: 8 total, 7 succeeded, 0 failed, 1 skipped (the existing symlink privilege capability skip).

The requested one full-suite run was:

```text
dotnet test FolderFolio.slnx
```

Result: 61 total, 59 succeeded, 1 failed, 1 skipped. The only failure remains the unrelated pre-existing `PhotoScannerTests.ScanAllAsync_indexes_only_immediate_supported_files_in_album_and_photo_order`, whose fixture cannot call `File.CreateSymbolicLink` because this Windows account lacks the required privilege.
