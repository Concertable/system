using Aspire.Hosting.DevTunnels;
using Concertable.B2B.Hosting.Frontend;
using Concertable.Customer.Hosting.Frontend;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Concertable.B2B.Hosting;
using Concertable.Customer.Hosting;
using Concertable.Frontend.Hosting;
using Microsoft.Extensions.Configuration;

namespace Concertable.AppHost;

public static class FrontendAppHostExtensions
{
    extension(IDistributedApplicationBuilder builder)
    {
        public IResourceBuilder<DevTunnelResource>? AddMobile(
            IResourceBuilder<IResourceWithServiceDiscovery> api,
            IResourceBuilder<IResourceWithServiceDiscovery> auth,
            IResourceBuilder<IResourceWithServiceDiscovery> searchWeb,
            IResourceBuilder<IResourceWithServiceDiscovery> customerWeb,
            IResourceBuilder<IResourceWithServiceDiscovery> paymentWeb)
        {
            if (!builder.Configuration.GetValue<bool>("RunMobile"))
                return null;

            var tunnel = builder.AddMobileTunnel("concertable-dev", auth, api, searchWeb, customerWeb, paymentWeb);
            builder.AddCustomerMobileSurface(api, auth, customerWeb, paymentWeb, tunnel)
                   .WithMobileServiceEndpoint(tunnel, new(searchWeb, "EXPO_PUBLIC_SEARCH_API_URL"));
            builder.AddB2BMobileSurface(api, auth, paymentWeb, tunnel)
                   .WithMobileServiceEndpoint(tunnel, new(searchWeb, "EXPO_PUBLIC_SEARCH_API_URL"))
                   .WithMobileServiceEndpoint(tunnel, new(customerWeb, "EXPO_PUBLIC_CUSTOMER_API_URL"));
            return tunnel;
        }
    }
}
