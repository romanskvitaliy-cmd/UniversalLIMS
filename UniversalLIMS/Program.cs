using Syncfusion.Licensing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Expert.Abstractions;
using UniversalLIMS.Application.Laboratory.Abstractions;
using UniversalLIMS.Application.Identity.Abstractions;
using UniversalLIMS.Application.Organization.Abstractions;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Application.Templates.Abstractions;
using UniversalLIMS.Domain.Identity;
using UniversalLIMS.Infrastructure.Persistence;
using UniversalLIMS.Infrastructure.Persistence.Interceptors;
using UniversalLIMS.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity.UI.Services;
using UniversalLIMS.Infrastructure.Expert;
using UniversalLIMS.Infrastructure.Identity;
using UniversalLIMS.Infrastructure.Laboratory;
using UniversalLIMS.Infrastructure.Organization;
using UniversalLIMS.Infrastructure.Registration;
using UniversalLIMS.Infrastructure.Security;
using UniversalLIMS.Infrastructure.Services;
using UniversalLIMS.Infrastructure.Templates;

namespace UniversalLIMS
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            string? syncfusionLicenseWarning = null;
            var syncfusionLicenseKey = builder.Configuration["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
            {
                SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
                if (!SyncfusionLicenseProvider.ValidateLicense(Platform.FileFormats, out var licenseMessage))
                {
                    syncfusionLicenseWarning =
                        "WARNING: Syncfusion:LicenseKey is not valid for the installed Syncfusion packages. " +
                        $"{licenseMessage} Word to PDF conversion may use evaluation watermarks until a valid key is configured.";
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

            if (builder.Environment.IsDevelopment())
            {
                // Ключі шифрування cookie за замовчуванням лише в пам'яті — після dotnet watch / F5
                // усі сесії стають недійсними. Зберігаємо їх поза bin/obj, щоб Clean/Rebuild у VS
                // не затирав ключі разом із каталогом збірки.
                var dataProtectionKeysPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "UniversalLIMS",
                    "DataProtection-Keys");
                Directory.CreateDirectory(dataProtectionKeysPath);

                var legacyKeysPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys");
                if (Directory.Exists(legacyKeysPath))
                {
                    foreach (var keyFile in Directory.GetFiles(legacyKeysPath, "key-*.xml"))
                    {
                        var destination = Path.Combine(dataProtectionKeysPath, Path.GetFileName(keyFile));
                        if (!File.Exists(destination))
                        {
                            File.Copy(keyFile, destination);
                        }
                    }
                }

                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
                    .SetApplicationName("UniversalLIMS");
            }

            builder.Services.AddOptions<LimsPortalOptions>()
                .Bind(builder.Configuration.GetSection(LimsPortalOptions.SectionName));

            builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = true;

                    // Development-only: allow simple role testing passwords.
                    // Your requested passwords (e.g. LIMS147) don't contain lowercase/special symbols.
                    if (builder.Environment.IsDevelopment())
                    {
                        options.Password.RequireNonAlphanumeric = false;
                        options.Password.RequireLowercase = false;
                        options.Password.RequireUppercase = false;
                    }
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddTransient<IEmailSender, IdentityEmailSender>();
            LimsIdentityCookieConfigurator.ConfigureLimsIdentityCookies(builder.Services, builder.Environment);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(8);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
            builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            builder.Services.AddScoped<IActiveLimsRoleService, ActiveLimsRoleService>();
            builder.Services.AddScoped<IPortalThemeService, PortalThemeService>();
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
            builder.Services.AddScoped<IBranchService, BranchService>();
            builder.Services.AddScoped<IUserManagementService, UserManagementService>();
            builder.Services.AddScoped<ICustomerService, CustomerService>();
            builder.Services.AddScoped<IOrderRegistrationService, OrderRegistrationService>();
            builder.Services.AddScoped<IOrderFieldLinkService, OrderFieldLinkService>();
            builder.Services.AddScoped<ILaboratoryBranchContext, LaboratoryBranchContext>();
            builder.Services.AddScoped<ILaboratoryOverviewService, LaboratoryOverviewService>();
            builder.Services.AddScoped<ILaboratoryJournalService, LaboratoryJournalService>();
            builder.Services.AddScoped<ILaboratoryPdfFillService, LaboratoryPdfFillService>();
            builder.Services.AddScoped<ILaboratoryDocumentSubmissionService, LaboratoryDocumentSubmissionService>();
            builder.Services.AddScoped<IOrderFieldValueService, OrderFieldValueService>();
            builder.Services.AddScoped<INumberingService, NumberingService>();
            builder.Services.AddScoped<IReferralPdfGenerator, ReferralPdfGenerator>();
            builder.Services.AddScoped<IPdfWorkspaceFillService, PdfWorkspaceFillService>();
            builder.Services.AddScoped<IFieldTextLibraryService, FieldTextLibraryService>();
            builder.Services.AddScoped<ITemplateFieldPermissionService, TemplateFieldPermissionService>();
            builder.Services.AddScoped<IExpertReviewQueueService, ExpertReviewQueueService>();
            builder.Services.AddScoped<IExpertPdfFillService, ExpertPdfFillService>();
            builder.Services.AddScoped<IExpertConclusionService, ExpertConclusionService>();
            builder.Services.AddScoped<ISampleDeliveryService, SampleDeliveryService>();
            builder.Services.AddScoped<IRegistrationNotificationService, RegistrationNotificationService>();
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

                options.AddPolicy(LimsPolicies.FillPdfWorkspace, policy =>
                    policy.RequireRole(LimsRoles.All));
            });
            builder.Services.AddAntiforgery(options =>
            {
                options.HeaderName = "RequestVerificationToken";
            });

            builder.Services.AddControllersWithViews()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                });

            var app = builder.Build();
            if (!string.IsNullOrWhiteSpace(syncfusionLicenseWarning))
            {
                app.Logger.LogWarning(syncfusionLicenseWarning);
            }

            await app.SeedLimsAsync();

            // One-off seed runner (dev/test):
            // dotnet run -- --seed-test-users
            var seedTestUsersOnly = args.Any(a => string.Equals(a, "--seed-test-users", StringComparison.OrdinalIgnoreCase));
            if (seedTestUsersOnly)
            {
                app.Logger.LogInformation("Seed-only mode enabled (--seed-test-users). Exiting without starting web server.");
                return;
            }

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

            app.UseSession();

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
