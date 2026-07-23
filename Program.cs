using System;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using InkPulse.Worker.Features.Auth.Constants;
using InkPulse.Worker.Features.Auth.Consumers;
using InkPulse.Worker.Features.Book.Consumers;
using BookQueueConstants = InkPulse.Worker.Features.Book.Constants.QueueConstants;
using InkPulse.Worker.Features.Order.Consumers;
using OrderQueueConstants = InkPulse.Worker.Features.Order.Constants.QueueConstants;
using InkPulse.Worker.Features.Coin.Jobs;
using InkPulse.Worker.Features.Coin.Options;
using InkPulse.Worker.Features.Coin.Services;
using InkPulse.Worker.Features.Book.Jobs;
using InkPulse.Worker.Features.Book.Options;
using InkPulse.Worker.Features.Book.Services;
using InkPulse.Worker.Infrastructure.Constants;
using InkPulse.Worker.Infrastructure.Services.Caching.Interfaces;
using InkPulse.Worker.Infrastructure.Services.Caching.Implementations;
using InkPulse.Worker.Infrastructure.Services.Caching.Models;
using InkPulse.Worker.Infrastructure.Services.Scheduling.Interfaces;
using InkPulse.Worker.Infrastructure.Services.Scheduling.Implementations;
using InkPulse.Worker.Infrastructure.Services.Security.Interfaces;
using InkPulse.Worker.Infrastructure.Services.Security.Implementations;
using InkPulse.Worker.Infrastructure.Services.Security.Models;
using InkPulse.Worker.Infrastructure.Services.Email.Interfaces;
using InkPulse.Worker.Infrastructure.Services.Email.Implementations;
using InkPulse.Worker.Infrastructure.Services.Email.Models;
using InkPulse.Worker.Infrastructure.Persistence;
using InkPulse.Worker.Infrastructure.Persistence.Implementations;
using InkPulse.Worker.Init;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Serilog;
using StackExchange.Redis;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting InkPulse Worker Host...");

    var builder = Host.CreateDefaultBuilder(args);

    builder.UseSerilog((hostingContext, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(hostingContext.Configuration)
            .Enrich.FromLogContext();
    });

    builder.ConfigureAppConfiguration((hostingContext, config) =>
    {
        config.Sources.Clear();
        config.SetBasePath(AppContext.BaseDirectory);
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        config.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false);
        config.AddEnvironmentVariables();
        if (args != null)
        {
            config.AddCommandLine(args);
        }
    });

    builder.ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;

        #region Database Configuration
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? "Host=write.db.inkpulse.com;Port=5432;Database=enterprise_db;Username=postgres;Password=AdminSecret123";

        services.AddDbContext<WorkerDbContext>(options =>
            options.UseNpgsql(connectionString, x => x.MigrationsAssembly(typeof(WorkerDbContext).Assembly.FullName)));
        services.AddTransient<IDapperRepository, DapperRepository>();
        #endregion

        #region Redis & Caching Configuration
        var cacheSection = configuration.GetSection(CacheProperties.SectionName);
        services.Configure<CacheProperties>(options =>
        {
            options.Redis = cacheSection["Redis"] ?? "";
            var sectionsSection = cacheSection.GetSection("Sections");
            foreach (var subSection in sectionsSection.GetChildren())
            {
                var children = subSection.GetChildren();
                bool hasChildren = false;
                foreach (var child in children)
                {
                    hasChildren = true;
                    var sectionKey = $"{subSection.Key}:{child.Key}";
                    var config = child.Get<CacheProperties.SectionConfig>();
                    if (config != null)
                    {
                        options.Sections[sectionKey] = config;
                    }
                }
                
                if (!hasChildren)
                {
                    var config = subSection.Get<CacheProperties.SectionConfig>();
                    if (config != null)
                    {
                        options.Sections[subSection.Key] = config;
                    }
                }
            }
        });

        var cacheProperties = new CacheProperties();
        cacheSection.Bind(cacheProperties);
        var sectionsSectionObj = cacheSection.GetSection("Sections");
        foreach (var subSection in sectionsSectionObj.GetChildren())
        {
            var children = subSection.GetChildren();
            bool hasChildren = false;
            foreach (var child in children)
            {
                hasChildren = true;
                var sectionKey = $"{subSection.Key}:{child.Key}";
                var config = child.Get<CacheProperties.SectionConfig>();
                if (config != null)
                {
                    cacheProperties.Sections[sectionKey] = config;
                }
            }
            
            if (!hasChildren)
            {
                var config = subSection.Get<CacheProperties.SectionConfig>();
                if (config != null)
                {
                    cacheProperties.Sections[subSection.Key] = config;
                }
            }
        }
        var redisConnectionString = !string.IsNullOrWhiteSpace(cacheProperties.Redis) 
                                   ? cacheProperties.Redis 
                                   : "redis-0.redis-headless.default.svc.cluster.local:6379,password=RedisSecret123";
        services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnectionString));
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<ISectionCacheService, SectionCacheService>();
        #endregion

        #region Quartz Scheduling Configuration
        services.AddQuartz(q =>
        {
            q.UseMicrosoftDependencyInjectionJobFactory();
        });
        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
        services.AddTransient(typeof(IScheduleService<>), typeof(ScheduleService<>));
        services.Configure<CoinBalanceBatchUpdateJobOptions>(configuration.GetSection(CoinBalanceBatchUpdateJobOptions.SectionName));
        services.AddTransient<CoinBalanceBatchUpdateJob>();
        services.AddHostedService<CoinBalanceBatchUpdateService>();
        services.Configure<FlashSaleJobOptions>(configuration.GetSection(FlashSaleJobOptions.SectionName));
        services.AddTransient<FlashSaleWarmUpJob>();
        services.AddHostedService<FlashSaleWarmUpService>();
        services.Configure<FlashSaleElsSyncOptions>(configuration.GetSection(FlashSaleElsSyncOptions.SectionName));
        services.AddTransient<FlashSaleElsSyncJob>();
        services.AddHostedService<FlashSaleElsSyncService>();
        #endregion

        #region Email Services
        services.Configure<GmailApiOptions>(configuration.GetSection(GmailApiOptions.SectionName));
        services.AddTransient<IGmailApiService, GmailApiService>();
        services.Configure<AesSettings>(configuration.GetSection(AesSettings.SectionName));
        services.AddSingleton<ICryptographyService, CryptographyService>();
        #endregion

        #region Elasticsearch Client
        var elasticUri = configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
        services.AddSingleton(new ElasticsearchClient(new Uri(elasticUri)));
        #endregion

        #region MassTransit & RabbitMQ
        services.AddMassTransit(x =>
        {
            x.AddConsumer<SendOtpEmailConsumer>();
            x.AddConsumer<SendNumberChallengeEmailConsumer>();
            x.AddConsumer<SendNewDeviceAlertConsumer>();
            x.AddConsumer<SendForgotPasswordEmailConsumer>();
            x.AddConsumer<SyncAuthorConsumer>();
            x.AddConsumer<SyncBookEditionConsumer>();
            x.AddConsumer<PartialUpdateBookEditionConsumer>();
            x.AddConsumer<SyncFlashSaleToElsConsumer>();
            x.AddConsumer<SyncPublisherNameConsumer>();
            x.AddConsumer<SyncCategorySlugConsumer>();
            x.AddConsumer<CreateGhnOrderConsumer>();
            x.AddConsumer<GhnStatusUpdateConsumer>();
            x.AddConsumer<SendOrderStatusEmailConsumer>();
            x.AddConsumer<PayOsWebhookConsumer>();
            x.AddConsumer<CancelGhnOrderConsumer>();
            x.AddConsumer<ReturnGhnOrderConsumer>();

            x.AddEntityFrameworkOutbox<WorkerDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox(bo =>
                {
                    bo.MessageDeliveryLimit = 10;
                    bo.MessageDeliveryTimeout = TimeSpan.FromSeconds(30);
                });
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(5);
            });
            x.UsingRabbitMq((context, cfg) =>
            {
                var hostName = configuration["RabbitMQ:Host"] ?? "rabbitmq-service";
                var port = ushort.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : (ushort)5672;
                var username = configuration["RabbitMQ:Username"] ?? "guest";
                var password = configuration["RabbitMQ:Password"] ?? "guest";

                cfg.Host(hostName, port, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ReceiveEndpoint(QueueConstants.SendOtpEmail, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<SendOtpEmailConsumer>(context);
                });

                cfg.ReceiveEndpoint(QueueConstants.SendChallengeEmail, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<SendNumberChallengeEmailConsumer>(context);
                });

                cfg.ReceiveEndpoint(QueueConstants.SendDeviceAlertEmail, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<SendNewDeviceAlertConsumer>(context);
                });

                cfg.ReceiveEndpoint(QueueConstants.SendForgotPasswordEmail, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<SendForgotPasswordEmailConsumer>(context);
                });

                cfg.ReceiveEndpoint(BookQueueConstants.SyncAuthor, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<SyncAuthorConsumer>(context);
                });

                cfg.ReceiveEndpoint(BookQueueConstants.SyncBookEdition, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<SyncBookEditionConsumer>(context);
                });

                cfg.ReceiveEndpoint(BookQueueConstants.SyncBookEditionPartial, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<PartialUpdateBookEditionConsumer>(context);
                });

                cfg.ReceiveEndpoint(BookQueueConstants.SyncFlashSaleToEls, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<SyncFlashSaleToElsConsumer>(context);
                });

                cfg.ReceiveEndpoint(BookQueueConstants.SyncPublisherName, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<SyncPublisherNameConsumer>(context);
                });

                cfg.ReceiveEndpoint(BookQueueConstants.SyncCategorySlug, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<SyncCategorySlugConsumer>(context);
                });

                cfg.ReceiveEndpoint(OrderQueueConstants.CreateGhnOrder, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<CreateGhnOrderConsumer>(context);
                });

                cfg.ReceiveEndpoint(OrderQueueConstants.GhnStatusUpdate, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<GhnStatusUpdateConsumer>(context);
                });

                cfg.ReceiveEndpoint(OrderQueueConstants.SendOrderConfirmationEmail, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<SendOrderStatusEmailConsumer>(context);
                });

                cfg.ReceiveEndpoint(OrderQueueConstants.PayOsWebhook, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<PayOsWebhookConsumer>(context);
                });

                cfg.ReceiveEndpoint(OrderQueueConstants.CancelGhnOrder, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<CancelGhnOrderConsumer>(context);
                });

                cfg.ReceiveEndpoint(OrderQueueConstants.ReturnGhnOrder, e =>
                {
                    e.UseRawJsonDeserializer();
                    e.ConfigureConsumer<ReturnGhnOrderConsumer>(context);
                });
            });
        });
        #endregion
    });

    var host = builder.Build();

    // Apply database migrations on startup
    DatabaseInitializer.ApplyMigrations(host.Services);

    // Initialize Elasticsearch indices and mappings on startup
    await ElasticsearchInitializer.InitializeIndicesAsync(host.Services);

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
