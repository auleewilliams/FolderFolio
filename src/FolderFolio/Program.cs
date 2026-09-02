using FolderFolio.Configuration;
using FolderFolio.Imaging;
using FolderFolio.Indexing;
using FolderFolio.Web;
using FolderFolio.Web.ViewModels;
using Microsoft.AspNetCore.Builder;
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
builder.Services.AddSingleton<PortfolioViewModelFactory>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(provider => new PhotoRootEventMapper(
    provider.GetRequiredService<FolderFolioOptions>().PhotoRoot));
builder.Services.AddSingleton<IndexRefreshCoordinator>();
builder.Services.AddSingleton<IPhotoRootWatcher, FileSystemPhotoRootWatcher>();
builder.Services.AddHostedService<IndexingService>();
builder.Services.Configure<ForwardedHeadersOptions>(ForwardedHeadersSetup.Configure);
builder.Services.AddRazorPages();

var app = builder.Build();

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

public partial class Program { }
