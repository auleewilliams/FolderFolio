using FolderFolio.Domain;
using FolderFolio.Imaging;
using FolderFolio.Indexing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FolderFolio.Tests.Support;

public sealed class FolderFolioWebApplicationFactory(
    PortfolioSnapshot snapshot,
    StubDerivativeService derivativeService) : WebApplicationFactory<Program>
{
    public IIndexRefreshQueue RefreshQueue => Services.GetRequiredService<IIndexRefreshQueue>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPortfolioIndex>();
            services.RemoveAll<IDerivativeService>();
            services.RemoveAll<IHostedService>();
            services.AddSingleton<IPortfolioIndex>(CreateIndex(snapshot));
            services.AddSingleton<IDerivativeService>(derivativeService);
        });
    }

    private static IPortfolioIndex CreateIndex(PortfolioSnapshot snapshot)
    {
        var index = new PortfolioIndex();
        index.PublishReady(snapshot, DateTimeOffset.UnixEpoch, TimeSpan.Zero);
        return index;
    }
}
