using FolderFolio.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Xunit;

namespace FolderFolio.Tests.Web;

public sealed class ForwardedHeadersSetupTests
{
    [Fact]
    public void Configure_trusts_one_loopback_bound_proxy_hop_for_address_and_scheme()
    {
        var options = new ForwardedHeadersOptions();

        ForwardedHeadersSetup.Configure(options);

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
    }
}
