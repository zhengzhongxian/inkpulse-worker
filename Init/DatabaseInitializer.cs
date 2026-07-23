using System;
using InkPulse.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Init
{
    public static class DatabaseInitializer
    {
        public static void ApplyMigrations(IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<WorkerDbContext>>();
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
                    logger.LogInformation("Applying database migrations...");
                    db.Database.Migrate();
                    logger.LogInformation("Database migrations applied successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while applying database migrations.");
                }
            }
        }
    }
}
