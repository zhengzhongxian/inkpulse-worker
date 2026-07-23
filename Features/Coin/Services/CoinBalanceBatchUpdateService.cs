using System;
using System.Threading;
using System.Threading.Tasks;
using InkPulse.Worker.Features.Coin.Jobs;
using InkPulse.Worker.Features.Coin.Options;
using InkPulse.Worker.Infrastructure.Services.Scheduling.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InkPulse.Worker.Features.Coin.Services
{
    public class CoinBalanceBatchUpdateService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly CoinBalanceBatchUpdateJobOptions _config;
        private readonly ILogger<CoinBalanceBatchUpdateService> _logger;

        public CoinBalanceBatchUpdateService(
            IServiceProvider serviceProvider,
            IOptions<CoinBalanceBatchUpdateJobOptions> configOptions,
            ILogger<CoinBalanceBatchUpdateService> logger)
        {
            _serviceProvider = serviceProvider;
            _config = configOptions.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.Enabled)
            {
                _logger.LogInformation("[CoinBalanceBatchUpdateService] CoinBalanceBatchUpdateJob is disabled");
                return;
            }

            _logger.LogInformation(
                "[CoinBalanceBatchUpdateService] Starting service. JobName={JobName}, CronExpression={CronExpression}",
                _config.JobName, _config.CronExpression);

            // Wait a brief delay on startup to allow core services to initialize
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            using var scope = _serviceProvider.CreateScope();
            var scheduleService = scope.ServiceProvider.GetRequiredService<IScheduleService<CoinBalanceBatchUpdateJob>>();

            try
            {
                await scheduleService.ScheduleJobAsync(
                    _config.JobName,
                    _config.CronExpression,
                    null,
                    stoppingToken
                );

                _logger.LogInformation("[CoinBalanceBatchUpdateService] CoinBalanceBatchUpdateJob scheduled successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CoinBalanceBatchUpdateService] Error scheduling CoinBalanceBatchUpdateJob");
            }
        }
    }
}
