using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System;
using System.IO;
using System.Net.Http;

namespace BasicRedisCacingExampleInDotNetCore
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllersWithViews();

            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/dist";
            });

            var redisEndpointUrl = (Configuration["REDIS_ENDPOINT_URL"]).Split(':');
            var redisHost = redisEndpointUrl[0];
            var redisPort = redisEndpointUrl[1];

            string redisConnectionUrl = string.Empty;
            
            var redisPassword = Configuration["REDIS_PASSWORD"];
            if (redisPassword != null)
            {
                redisConnectionUrl = $"{redisHost}:{redisPort},password={redisPassword},ssl=True,abortConnect=False";
            }
            else
            {
                redisConnectionUrl = $"{redisHost}:{redisPort},ssl=True,abortConnect=False";
            }
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("dotnet");
            client.BaseAddress = new Uri("https://api.github.com");
            services.AddSingleton(client);
            services.AddSingleton(Configuration);
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionUrl));
            services.AddApplicationInsightsTelemetry(Configuration["APPINSIGHTS_CONNECTIONSTRING"]);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            app.UseSpaStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {

            });

            app.Map(new PathString(""), client =>
            {
                var clientPath = Path.Combine(Directory.GetCurrentDirectory(), "./ClientApp/dist");
                StaticFileOptions clientAppDist = new StaticFileOptions()
                {
                    FileProvider = new PhysicalFileProvider(clientPath)
                };
                client.UseSpaStaticFiles(clientAppDist);
                client.UseSpa(spa => { spa.Options.DefaultPageStaticFileOptions = clientAppDist; });

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllerRoute(name: "default", pattern: "{controller}/{action=Index}/{id?}");
                });
            });

            /*app.UseSpa(spa =>
            {
                spa.Options.SourcePath = "ClientApp";
            });*/
        }
    }
}
