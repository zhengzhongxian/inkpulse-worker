using System;
using System.Threading;
using System.Threading.Tasks;
using InkPulse.Worker.Infrastructure.Services.Scheduling.Interfaces;
using Microsoft.Extensions.Logging;
using Quartz;

namespace InkPulse.Worker.Infrastructure.Services.Scheduling.Implementations
{
    public class ScheduleService<TJob> : IScheduleService<TJob> where TJob : IJob
    {
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly ILogger<ScheduleService<TJob>> _logger;

        public ScheduleService(
            ISchedulerFactory schedulerFactory,
            ILogger<ScheduleService<TJob>> logger)
        {
            _schedulerFactory = schedulerFactory;
            _logger = logger;
        }

        private async Task<IScheduler> GetScheduler(CancellationToken cancellationToken)
        {
            return await _schedulerFactory.GetScheduler(cancellationToken);
        }
        
        private static JobKey GetJobKey(string jobName)
        {
            var group = typeof(TJob).Name;
            return new JobKey(jobName, group);
        }

        public async Task ScheduleJobAsync(
            string jobName,
            string cronExpression,
            JobDataMap? jobData = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Scheduling job {JobName} with cron expression {CronExpression}",
                    jobName, cronExpression);

                var scheduler = await GetScheduler(cancellationToken);

                var jobKey = GetJobKey(jobName);

                var jobBuilder = JobBuilder.Create<TJob>()
                    .WithIdentity(jobKey)
                    .WithDescription($"Job for {typeof(TJob).Name}");

                if (jobData is not null)
                {
                    jobBuilder.UsingJobData(jobData);
                }

                var jobDetail = jobBuilder
                    .StoreDurably()
                    .Build();

                var triggerKey = new TriggerKey($"{jobKey.Name}-trigger", jobKey.Group);
                var trigger = TriggerBuilder.Create()
                    .WithIdentity(triggerKey)
                    .WithCronSchedule(cronExpression)
                    .ForJob(jobKey)
                    .Build();

                await scheduler.AddJob(jobDetail, true, cancellationToken);

                if (await scheduler.CheckExists(triggerKey, cancellationToken))
                {
                    await scheduler.RescheduleJob(triggerKey, trigger, cancellationToken);
                    _logger.LogInformation("Rescheduled job {JobName} successfully", jobName);
                }
                else
                {
                    await scheduler.ScheduleJob(trigger, cancellationToken);
                    _logger.LogInformation("Scheduled job {JobName} successfully", jobName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to schedule job {JobName} with cron expression {CronExpression}, Error: {ex}",
                    jobName, cronExpression, ex);
                throw;
            }
        }

        public async Task<bool> DeleteJobAsync(
            string jobName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Deleting job {JobName}", jobName);
                var scheduler = await GetScheduler(cancellationToken);
                var jobKey = GetJobKey(jobName);

                if (await scheduler.CheckExists(jobKey, cancellationToken))
                {
                    var result = await scheduler.DeleteJob(jobKey, cancellationToken);
                    if (result)
                    {
                        _logger.LogInformation("Deleted job {JobName} successfully", jobName);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to delete job {JobName}", jobName);
                    }
                    return result;
                }

                _logger.LogWarning("Job {JobName} not found for deletion", jobName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to delete job {JobName}. Error: {ex}", jobName, ex);
                throw;
            }
        }

        public async Task TriggerJobAsync(
            string jobName,
            JobDataMap? jobData = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Triggering job {JobName}", jobName);
                var scheduler = await GetScheduler(cancellationToken);
                var jobKey = GetJobKey(jobName);

                if (!await scheduler.CheckExists(jobKey, cancellationToken))
                {
                    _logger.LogWarning("Job {JobName} not found for triggering", jobName);
                    return;
                }

                if (jobData is not null)
                {
                    await scheduler.TriggerJob(jobKey, jobData, cancellationToken);
                }
                else
                {
                    await scheduler.TriggerJob(jobKey, cancellationToken);
                }

                _logger.LogInformation("Triggered job {JobName} successfully", jobName);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to trigger job {JobName}. Error: {ex}", jobName, ex);
                throw;
            }
        }
    }
}
