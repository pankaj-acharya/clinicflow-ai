using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
}
