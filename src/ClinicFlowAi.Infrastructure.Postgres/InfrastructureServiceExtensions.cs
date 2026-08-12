using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ClinicFlowAi.Infrastructure.Postgres.Persistence;

namespace ClinicFlowAi.Infrastructure.Postgres;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddPostgresInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ClinicFlowDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IAppointmentRepository, AppointmentRepository>();

        return services;
    }

    /// <summary>
    /// Runs EF migrations and seeds development data if the database is empty.
    /// Call from Program.cs after app.Build() in Development environments only.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this IHost app)
    {
        using var scope = app.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<ClinicFlowDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ClinicFlowDbContext>>();

        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db, logger);
    }
}
