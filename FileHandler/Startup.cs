using Hangfire;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(ProxyFileHandler.Startup))]

namespace ProxyFileHandler
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            GlobalConfiguration.Configuration
                .UseSqlServerStorage("Hangfire");
            
            app.UseHangfireDashboard();
            app.UseHangfireServer(new BackgroundJobServerOptions
            {
                Queues = new[] { nameof(ProxyFileHandler) }
            });
        }
    }
}