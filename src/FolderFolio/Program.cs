using FolderFolio.Configuration;
using FolderFolio.Imaging;
using FolderFolio.Indexing;
using FolderFolio.Web;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IValidateOptions<FolderFolioOptions>, FolderFolioOptionsValidator>();
builder.Services.AddOptions<FolderFolioOptions>()
    .Bind(builder.Configuration.GetSection(FolderFolioOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton(provider => provider.GetRequiredService<IOptions<FolderFolioOptions>>().Value);
builder.Services.AddSingleton<IPortfolioIndex, PortfolioIndex>();
builder.Services.AddSingleton<IPhotoScanFileSystem, PhotoScanFileSystem>();
builder.Services.AddSingleton<IImageMetadataReader, ImageSharpMetadataReader>();
builder.Services.AddSingleton<IPhotoScanner, PhotoScanner>();
builder.Services.AddSingleton<IIndexRefreshQueue, IndexRefreshQueue>();
builder.Services.AddSingleton<IDerivativeKeyFactory, DerivativeKeyFactory>();
builder.Services.AddSingleton<ISourcePathGuard, SourcePathGuard>();
builder.Services.AddSingleton<IImageDerivativeGenerator, ImageSharpDerivativeGenerator>();
builder.Services.AddSingleton<IDerivativeService, DerivativeService>();
builder.Services.AddSingleton<IMediaUrlBuilder, MediaUrlBuilder>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(provider => new PhotoRootEventMapper(
    provider.GetRequiredService<FolderFolioOptions>().PhotoRoot));
builder.Services.AddSingleton<IndexRefreshCoordinator>();
builder.Services.AddSingleton<IPhotoRootWatcher, FileSystemPhotoRootWatcher>();
builder.Services.AddHostedService<IndexingService>();
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapGet(
    "/media/{albumSlug}/{photoId}/{size}",
    MediaEndpoint.HandleAsync)
   .WithName(MediaEndpoint.RouteName);

app.Run();

public partial class Program { }
