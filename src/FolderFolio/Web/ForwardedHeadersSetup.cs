using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace FolderFolio.Web;

public static class ForwardedHeadersSetup
{
    public static void Configure(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;

        // Compose binds the host tunnel to 127.0.0.1; non-loopback deployments must configure explicit known proxy addresses or networks.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
    }
}
