using Microsoft.EntityFrameworkCore;

namespace UniversalLIMS.Infrastructure.Persistence.Seed;

public static class SeedDataExtensions
{
    public static async Task SeedLimsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        var seeder = services.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync();

        await LaboratoryDataSeeder.SeedAsync(context);
        await ProtocolTagCatalogSeeder.SeedAsync(context);
    }
}
