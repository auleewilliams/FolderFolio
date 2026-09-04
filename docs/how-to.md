# FolderFolio quick how-to

FolderFolio turns folders of photos into a self-hosted portfolio. You manage the site by adding, renaming, replacing, or removing image files; there is no admin interface or database.

## Start the portfolio

1. Install Docker Engine with the Compose plugin.
2. From the repository root, create an album and start the service:

   ```sh
   mkdir -p photos/01-Landscapes
   docker compose up -d --build
   ```

3. Open `http://127.0.0.1:8080` in a browser.

FolderFolio listens only on your computer's loopback address. If you use a host-based Cloudflare Tunnel, set its origin to `http://localhost:8080`.

## Add photos and albums

Create one directory per album directly inside `photos`, then place images directly in that directory:

```text
photos/
  01-Landscapes/
    sunrise.jpg
    coast.webp
  02-Portraits/
    alex.png
```

Album prefixes such as `01-` or `02_` control display order and are omitted from the displayed title. FolderFolio supports `.jpg`, `.jpeg`, `.png`, and `.webp` files. Nested directories are ignored.

Changes to supported photo files are indexed automatically. To update the portfolio, simply add, replace, rename, or remove a photo; no restart is required.

## Check that it is working

```sh
curl -i http://127.0.0.1:8080/health
docker compose logs --no-color folderfolio
```

The health endpoint returns `200` when the photo index is ready. It can return `503` briefly while the initial scan runs or if the latest scan has a problem.

## Run locally without Docker

Install the .NET SDK specified in `global.json`, create a photo directory and a writable cache directory, then run:

```sh
dotnet run --project src/FolderFolio -- --FolderFolio:PhotoRoot=/absolute/path/to/photos --FolderFolio:CacheRoot=/absolute/path/to/cache
```

For all configuration options and operational notes, see the [README](../README.md).
