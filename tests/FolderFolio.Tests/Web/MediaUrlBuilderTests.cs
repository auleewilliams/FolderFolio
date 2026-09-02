using System.Collections.Immutable;
using FolderFolio.Domain;
using FolderFolio.Imaging;
using FolderFolio.Tests.Support;
using FolderFolio.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FolderFolio.Tests.Web;

public sealed class MediaUrlBuilderTests
{
    [Fact]
    public void Build_creates_an_encoded_named_media_route_with_the_current_derivative_version()
    {
        var photo = new IndexedPhoto("opaque/photo", "coast.jpg", new SourceFingerprint("01 Landscapes/coast.jpg", 3, 123), null, 120, 80);
        var album = new IndexedAlbum("01 Landscapes", "landscapes", "Landscapes", "landscapes", 1, ImmutableArray.Create(photo));
        var keyFactory = new DerivativeKeyFactory(new FolderFolio.Configuration.FolderFolioOptions());
        using var app = new FolderFolioWebApplicationFactory(
            new PortfolioSnapshot(ImmutableArray.Create(album)),
            new StubDerivativeService { Derivative = new CachedDerivative("unused", 0, DateTimeOffset.UnixEpoch) });
        var builder = app.Services.GetRequiredService<IMediaUrlBuilder>();

        var url = builder.Build(album, photo, DerivativeKind.Grid);

        Assert.Equal($"/media/landscapes/opaque%2Fphoto/grid?v={keyFactory.Create(photo, DerivativeKind.Grid).Version}", url);
    }
}
