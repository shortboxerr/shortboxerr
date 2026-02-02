using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shortboxerr.Infrastructure.Persistence;

namespace Shortboxerr.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ShortboxerrDbContext>(options =>
            options.UseSqlite(connectionString));

        return services;
    }
}

