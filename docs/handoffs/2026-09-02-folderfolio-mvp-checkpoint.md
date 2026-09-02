# FolderFolio MVP implementation checkpoint

## Repository state

- Worktree: `/Users/leew/Code/FolderFolio/.worktrees/folderfolio-mvp`
- Branch: `codex/folderfolio-mvp`
- Last accepted commit before this checkpoint: `a95d705041b20c398246925b0d1dab106a77cbbc` (`fix: harden photo scanner boundaries`)
- This checkpoint commits the complete current Task 4 work, which has passed its local tests but has **not** yet received its independent Task 4 review.

## Completed and accepted

1. .NET 10 Razor Pages scaffold, central dependency management, options validation, lock files, and MTP test-runner configuration.
2. Immutable portfolio domain, album parsing/collisions, photo identity, and atomic index publication.
3. ImageSharp metadata scanning and filesystem albums, including corrupt-file isolation, exact supported extension boundaries, reparse protection, album-level I/O isolation, and targeted rescans.

The approved design is in `docs/superpowers/specs/2026-09-01-folderfolio-mvp-design.md`; the 12-task implementation plan is in `docs/superpowers/plans/2026-09-01-folderfolio-mvp.md`. The detailed execution ledger, per-task reports, review packages, and reviewer results live under `.superpowers/sdd/2026-09-01-folderfolio-mvp/` and are intentionally ignored by Git.

## Current Task 4 checkpoint

The working tree now contains the Task 4 event/indexing pipeline:

- bounded, nonblocking `IndexRefreshQueue` that forces a full scan when it overflows;
- `PhotoRootEventMapper` that maps only direct album photos to album directory names, treats top-level album changes as full scans, ignores nested paths, and handles both sides of renames;
- debounced `IndexRefreshCoordinator` using `TimeProvider`, with targeted/full scan selection, publication, degradation, and safe aggregate logging;
- `FileSystemPhotoRootWatcher` and `IndexingService` for watcher lifecycle, unavailable-root retries, startup scans, and refresh processing;
- dependency-injection registrations in `Program.cs`;
- queue/mapper/coordinator tests and a stub scanner.

The implementation worker was intentionally interrupted after this code was written. Do not treat Task 4 as accepted yet: first check it against the exact task brief, write/finish its durable report, run an independent review, and perform a fix loop if needed.

## Verification recorded at the checkpoint

- Focused Task 4 tests: `dotnet test FolderFolio.slnx --filter "FullyQualifiedName~IndexRefresh|FullyQualifiedName~PhotoRootEventMapper"` — **9 passed**.
- Full suite: `dotnet test FolderFolio.slnx` — **42 passed**.
- Build: `dotnet build FolderFolio.slnx --no-restore --disable-build-servers -m:1 -warnaserror` — **0 warnings, 0 errors**.

## Important continuation notes

- `SixLabors.ImageSharp` is intentionally pinned to **3.1.12**, not the planned 4.1.1. Six Labors 4.x requires a user-supplied license key/file and emits a warning without one; 3.1.12 was the latest compatible 3.x release with a clean, locked, warning-free build. The full rationale is in `.superpowers/sdd/2026-09-01-folderfolio-mvp/progress.md`.
- In this environment, `dotnet test` needs local named-pipe IPC outside the restrictive sandbox. When sandboxed it can hang; the exact normal commands complete quickly with the appropriate local permission. Do **not** append `--no-restore` to `dotnet test`: .NET 10 MTP can falsely report zero discovered tests in that variant.
- Do not delete the `.superpowers/sdd` workspace until every task has passed its review. It carries the execution ledger and prior decision record, although it is deliberately Git-ignored.
- After Task 4 acceptance, resume with Task 5 (derivative identity and cache path guard) from `docs/superpowers/plans/2026-09-01-folderfolio-mvp.md`.
