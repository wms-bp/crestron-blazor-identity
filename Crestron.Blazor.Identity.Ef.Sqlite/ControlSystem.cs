using Crestron.Blazor.Identity.Ef.Sqlite.Components;
using Crestron.Blazor.Identity.Ef.Sqlite.Components.Account;
using Crestron.Blazor.Identity.Ef.Sqlite.Data;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Radzen;
using SQLitePCL;

namespace Crestron.Blazor.Identity.Ef.Sqlite;

public class ControlSystem : CrestronControlSystem
{
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

                // Load static web assets manifest
                StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

                builder.WebHost.ConfigureKestrel(serverOptions =>
                {
                    serverOptions.Listen(System.Net.IPAddress.Parse(CrestronEthernetHelper.GetEthernetParameter(
                        CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0)), 7070);
                });

                // Add services to the container.
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
                var mainProjectPath = "/user";
                var dbPath = Path.Combine(mainProjectPath, "app.db");
                var connectionString = $"Data Source={dbPath}";
                
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlite(connectionString));

                // Ensure database is created and migrated
                using (var scope = builder.Services.BuildServiceProvider().CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    try
                    {
                        dbContext.Database.EnsureCreated();
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

// Configure the HTTP request pipeline.
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
                var wwwbaseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var wwwrootPath = Path.Combine(wwwbaseDir ?? "", "wwwroot");

                // First, add the physical wwwroot directory
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

                // Add additional endpoints required by the Identity /Account Razor components.
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
    

  