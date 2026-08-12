using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClinicFlowAi.Infrastructure.Postgres.Persistence;

/// <summary>
/// Design-time factory for the DbContext, used by EF Core tools (migrations, etc.)
/// to instantiate the context without needing a full DI container.
/// </summary>
public sealed class ClinicFlowDbContextFactory : IDesignTimeDbContextFactory<ClinicFlowDbContext>
{
    public ClinicFlowDbContext CreateDbContext(string[] args)
    {
        // Default development connection string for migrations.
        // This will be overridden at runtime via dependency injection when
        // AddPostgresInfrastructure is called with an actual connection string.
        const string defaultConnection = "Host=localhost;Database=clinicflow;Username=clinicadmin;Password=localdev;SslMode=Disable";

        var optionsBuilder = new DbContextOptionsBuilder<ClinicFlowDbContext>();
        optionsBuilder.UseNpgsql(defaultConnection);

        return new ClinicFlowDbContext(optionsBuilder.Options);
    }
}
