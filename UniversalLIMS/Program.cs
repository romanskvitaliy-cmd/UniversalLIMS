using Syncfusion.Licensing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Identity;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Persistence.Interceptors;
using UniversalLIMS.Infrastructure.Persistence.Seed;
using UniversalLIMS.Infrastructure.Registration;
using UniversalLIMS.Infrastructure.Services;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var syncfusionLicenseKey = builder.Configuration["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
            {
                SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
                if (!SyncfusionLicenseProvider.ValidateLicense(Platform.FileFormats, out var licenseMessage))
                {
                    Console.WriteLine(
                        "WARNING: Syncfusion:LicenseKey is not valid for the installed Syncfusion packages. " +
                        $"{licenseMessage} Word to PDF conversion may use evaluation watermarks until a valid key is configured.");
                }
            }

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddScoped<SoftAnnulmentSaveChangesInterceptor>();
            builder.Services.AddScoped<AuditSaveChangesInterceptor>();
            builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
                options
                    .UseSqlServer(connectionString)
                    .AddInterceptors(
                        serviceProvider.GetRequiredService<SoftAnnulmentSaveChangesInterceptor>(),
                        serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            builder.Services.AddScoped<ISystemOperationContext, SystemOperationContext>();
            builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
            builder.Services.AddScoped<DataSeeder>();
            builder.Services.AddScoped<IDocxContentControlReader, ZipDocxContentControlReader>();
            builder.Services.AddSingleton<IWordToPdfDocumentConverter>(serviceProvider =>
                WordToPdfDocumentConverterFactory.Create(
                    serviceProvider.GetRequiredService<IConfiguration>(),
                    serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("WordToPdf")));
            builder.Services.AddScoped<ITemplateDocumentStorage, LocalTemplateDocumentStorage>();
            builder.Services.AddScoped<ITemplatePublicationValidator, TemplatePublicationValidator>();
            builder.Services.AddScoped<ITemplateVersionService, TemplateVersionService>();
            builder.Services.AddScoped<ITemplateFieldMappingService, TemplateFieldMappingService>();
            builder.Services.AddSingleton<ITemplateOriginalOpenTokenIssuer, TemplateOriginalOpenTokenIssuer>();
            builder.Services.AddScoped<ICustomerService, CustomerService>();
            builder.Services.AddScoped<IOrderFieldValueService, OrderFieldValueService>();
            builder.Services.AddScoped<INumberingService, NumberingService>();
            builder.Services.AddScoped<IReferralPdfGenerator, ReferralPdfGenerator>();
            builder.Services.AddScoped<IPdfWorkspaceFillService, PdfWorkspaceFillService>();
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(LimsPolicies.ManageSystem, policy =>
                    policy.RequireRole(LimsRoles.SystemAdministrator));

                options.AddPolicy(LimsPolicies.RegisterSamples, policy =>
                    policy.RequireRole(LimsRoles.SystemAdministrator, LimsRoles.Registrar));

                options.AddPolicy(LimsPolicies.EnterLaboratoryResults, policy =>
                    policy.RequireRole(LimsRoles.SystemAdministrator, LimsRoles.LaboratoryTechnician));

                options.AddPolicy(LimsPolicies.ApproveConclusions, policy =>
                    policy.RequireRole(LimsRoles.SystemAdministrator, LimsRoles.Specialist));
            });
            builder.Services.AddControllersWithViews()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                });

            var app = builder.Build();

            await app.SeedLimsAsync();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            app.MapControllers();
            app.MapRazorPages();

            await app.RunAsync();
        }
    }
}
