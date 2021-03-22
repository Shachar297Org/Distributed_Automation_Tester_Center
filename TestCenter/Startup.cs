using Console;
using Console.Interfaces;
using Console.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http;
using TestCenter.Hubs;
using TestCenter.LiteDb;
using TestCenterConsole.Utilities;
using Newtonsoft.Json;

namespace TestCenter
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            
            services.AddSignalR();

            services.AddHttpClient();

            services.AddSingleton<ILiteDbContext, LiteDbContext>();
            services.AddTransient<ITestCenterService, TestCenterService>();

            services.AddSingleton<Settings>(x => Settings.GetInstance(Configuration["TestCenter:ConfigPath"]));
            services.AddSingleton<IBackEndInterface, BackEnd>();

            //var sp = services.BuildServiceProvider();
            //var settings = sp.GetService<Settings>();

            //services.AddHttpClient("lumenis_api", c =>
            //{
            //    c.BaseAddress = new System.Uri(settings["API_LOGIN_HOST"]);
            //}).ConfigurePrimaryHttpMessageHandler(() =>
            //{
            //    return new HttpClientHandler()
            //    {
            //        UseDefaultCredentials = true,
            //        Credentials = new NetworkCredential(settings["API_USER"], settings["API_PASS"]),
                    
            //    };
            //});

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=UI}/{action=Index}/{id?}");

                endpoints.MapHub<TestCenterHub>("/tchub");
            });

            app.Use((context, next) =>
            {
                context.Request.EnableBuffering(); 
                return next();
            });
        }
    }
}
