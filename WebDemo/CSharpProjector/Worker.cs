using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpProjector.Infrastructure;
using CSharpProjector.Projectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Propulsion.Cosmos;

namespace CSharpProjector
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly CosmosContext _context;
        public Worker(ILogger<Worker> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _context = ConfigureStore(_configuration.GetSection("SummaryProjector"));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _context.ProjectorHandler = new SummaryProjector();
            await _context.StartPipeline();
            await _context.RunProjector(stoppingToken);
        }

        private CosmosContext ConfigureStore(IConfigurationSection configuration)
        {
            const string connVar = "CosmosConnection";
            var conn = configuration[connVar];
            const string dbVar = "CosmosDb";
            var db = configuration[dbVar];
            const string collVar = "CosmosCollection";
            var coll = configuration[collVar];
            if (conn == null || db == null || coll == null)
                throw new Exception(
                    $"Event Storage subsystem requires the following Environment Variables to be specified: {connVar} {dbVar}, {collVar}");

            const string timeoutVar = "CosmosTimeout";
            var timeout = configuration.GetValue<int>(timeoutVar);

            const string groupNameVar = "ConsumerGroupName";
            string groupName = configuration[groupNameVar];
            const string leaseSuffixVar = "LeaseContainerSuffix";
            string leaseSuffix = configuration[leaseSuffixVar];

            var connMode = Equinox.Cosmos.ConnectionMode.Direct;
            var cosmosConfig = new CosmosConfig(connMode, conn, db, coll, timeout);
            var config = new CosmosChangeFeedConfig(groupName, leaseSuffix, cosmosConfig);
            return new CosmosContext(config);
        }
    }
}
