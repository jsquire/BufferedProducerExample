using System;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BufferedProducerUserTelemetry
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        private TelemetryPublisher TelemetryPublisher { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            // Create the producer to use for telemetry-related publishing.

            var producerOptions = new EventHubBufferedProducerClientOptions
            {
                Identifier = "Telemetry Producer",
                RetryOptions = new EventHubsRetryOptions { MaximumRetries = 25, TryTimeout = TimeSpan.FromMinutes(5) }
            };

            TelemetryPublisher = new TelemetryPublisher(Configuration["EventHubsConnectionString"], Configuration["EventHubName"], producerOptions);
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            // Register the producer as a singleton, so that it is available in the global
            // context.

            services.AddSingleton(TelemetryPublisher.Producer);

            // Extend the application shutdown so that any buffered events have
            // time to be published, including accommodating retries.

            services.Configure<HostOptions>(options =>
            {
                options.ShutdownTimeout = TimeSpan.FromMinutes(15);
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime, ILogger<Startup> logger)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });

            // Configure the logger to be used for reporting on publishing
            // operations.

            TelemetryPublisher.SetLogger(logger);

            // Configure the host to stop the producer when shutting down; this
            // will ensure that buffered events are published.

            lifetime.ApplicationStopping.Register(async () =>
            {
                logger.LogInformation($"The { nameof(TelemetryPublisher) } is closing; there are { TelemetryPublisher.Producer.TotalBufferedEventCount } buffered events that will be published.");
                await TelemetryPublisher.Producer.CloseAsync();
                logger.LogInformation($"The { nameof(TelemetryPublisher) } has been closed.");
            },
            useSynchronizationContext: false);
        }
    }
}
