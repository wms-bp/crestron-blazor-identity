using System;
using System.IO;
using System.Threading.Tasks;
using Crestron.Blazor.Identity.Ef.Sqlite.Components;
using Crestron.Blazor.Identity.Ef.Sqlite.Components.Account;
using Crestron.Blazor.Identity.Ef.Sqlite.Data;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Radzen;
using SQLitePCL;

namespace Crestron.Blazor.Identity.Ef.Sqlite;

public class ControlSystem : CrestronControlSystem
{
    private const string DbBasePath = "/user";
    private const string DbFileName = "app.db";
    
    public ControlSystem()
    {

    }

    public override void InitializeSystem()
    {
        Task.Run(() =>
        {
            try
            {
                // Force SQLitePCL to use the native SQLite3 library
                raw.SetProvider(new SQLite3Provider_sqlite3());
                raw.FreezeProvider();
                
                var builder = WebApplication.CreateBuilder();

                /*
                 Use static web assets. Ensures static assets from _content are loaded from wwwroot from CPZ extraction
                 See CSPROJ file for PropertyGroup with StaticWebAssets for more details
                 */
                StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

                builder.WebHost.ConfigureKestrel(serverOptions =>
                {
                    serverOptions.Listen(System.Net.IPAddress.Parse(CrestronEthernetHelper.GetEthernetParameter(
                        CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0)), 7070);
                });

                
                builder.Services.AddRazorComponents()
                    .AddInteractiveServerComponents();
                builder.Services.AddRadzenComponents();

                builder.Services.AddCascadingAuthenticationState();
                builder.Services.AddScoped<IdentityUserAccessor>();
                builder.Services.AddScoped<IdentityRedirectManager>();
                builder.Services
                    .AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

                builder.Services.AddAuthentication(options =>
                    {
                        options.DefaultScheme = IdentityConstants.ApplicationScheme;
                        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
                    })
                    .AddIdentityCookies();

                var dbPath = Path.Combine(DbBasePath, DbFileName);
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath}"));

                
                using (var scope = builder.Services.BuildServiceProvider().CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    try
                    {
                        dbContext.Database.Migrate();
                        CrestronConsole.PrintLine("Database auto-migration completed successfully.");
                    }
                    catch (Exception ex)
                    {
                       ErrorLog.Error($"Error during database auto-migration: {ex.Message}");
                    }
                }

                builder.Services.AddDatabaseDeveloperPageExceptionFilter();

                builder.Services
                    .AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                    .AddEntityFrameworkStores<ApplicationDbContext>()
                    .AddSignInManager()
                    .AddDefaultTokenProviders();

                builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

                var app = builder.Build();


                if (app.Environment.IsDevelopment())
                {
                    app.UseMigrationsEndPoint();
                }
                else
                {
                    app.UseExceptionHandler("/Error", createScopeForErrors: true);
                    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                    app.UseHsts();
                }

                //app.UseHttpsRedirection();
                var wwwrootPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "wwwroot");

                // Add the physical wwwroot directory for the static web assets from StaticWebAssetsLoader
                if (Directory.Exists(wwwrootPath))
                {
                    app.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new PhysicalFileProvider(wwwrootPath),
                        RequestPath = ""
                    });
                }

                app.UseStaticFiles();
                app.UseAntiforgery();

                app.MapRazorComponents<App>()
                    .AddInteractiveServerRenderMode();

                // Add additional endpoints required by the Identity/Account Razor components.
                app.MapAdditionalIdentityEndpoints();

                app.Run();
            }
            catch (Exception exception)
            {
                ErrorLog.Error($"Program Load Exception | {exception.Message}");
            }
        });
    }
}
    

  