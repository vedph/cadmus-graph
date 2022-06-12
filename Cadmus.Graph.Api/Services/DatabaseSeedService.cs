using Cadmus.Graph.MySql;
using Fusi.DbManager.MySql;
using Polly;
using System.Data.Common;

namespace Cadmus.Graph.Api.Services
{
    /// <summary>
    /// Database seed service.
    /// See https://andrewlock.net/running-async-tasks-on-app-startup-in-asp-net-core-3.
    /// </summary>
    /// <seealso cref="IHostedService" />
    public sealed class DatabaseSeedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public DatabaseSeedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private static void SeedGraphDatabase(
            IConfiguration config,
            ILogger? logger)
        {
            // nope if database exists
            string cst = config.GetConnectionString("Template");
            string db = config.GetValue<string>("DatabaseName");

            MySqlDbManager dbManager = new(cst);
            if (dbManager.Exists(db))
            {
                logger?.LogInformation($"Database {db} exists");
                return;
            }

            // else create and seed it
            logger?.LogInformation($"Creating database {db}");
            dbManager.CreateDatabase(db, MySqlGraphRepository.GetSchema(), null);
        }

        private static Task SeedGraphDatabaseAsync(IServiceProvider serviceProvider)
        {
            return Policy.Handle<DbException>()
                .WaitAndRetry(new[]
                {
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(60)
                }, (exception, timeSpan, _) =>
                {
                    ILogger? logger = serviceProvider
                        .GetService<ILoggerFactory>()?
                        .CreateLogger(typeof(DatabaseSeedService));

                    string message = "Unable to connect to DB" +
                        $" (sleep {timeSpan}): {exception.Message}";
                    Console.WriteLine(message);
                    logger?.LogError(exception, message);
                }).Execute(() =>
                {
                    IConfiguration config =
                        serviceProvider.GetService<IConfiguration>()!;

                    ILogger? logger = serviceProvider
                        .GetService<ILoggerFactory>()?
                        .CreateLogger(typeof(DatabaseSeedService));

                    Console.WriteLine("Seeding database...");
                    SeedGraphDatabase(config, logger);
                    Console.WriteLine("Seeding completed");
                    return Task.CompletedTask;
                });
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            IServiceProvider serviceProvider = scope.ServiceProvider;

            try
            {
                await SeedGraphDatabaseAsync(serviceProvider);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                ILogger? logger = serviceProvider.GetService<ILoggerFactory>()!
                    .CreateLogger(typeof(DatabaseSeedService));
                logger.LogError(ex, ex.Message);
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
