using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Equinox.Cosmos;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using Propulsion;
using Propulsion.Cosmos;
using Propulsion.Streams;
using Serilog;
using Log = Serilog.Log;

namespace CSharpProjector.Infrastructure
{
    public class CosmosConfig
    {
        public CosmosConfig(ConnectionMode mode, string connectionStringWithUriAndKey, string database,
            string container)
        {
            Mode = mode;
            ConnectionStringWithUriAndKey = connectionStringWithUriAndKey;
            Database = database;
            Container = container;
            Timeout = 5;
        }
        public CosmosConfig(ConnectionMode mode, string connectionStringWithUriAndKey, string database,
            string container, int? timeout)
        {
            Mode = mode;
            ConnectionStringWithUriAndKey = connectionStringWithUriAndKey;
            Database = database;
            Container = container;
            Timeout = timeout ?? 5;
        }

        public ConnectionMode Mode { get; }
        public string ConnectionStringWithUriAndKey { get; }
        public string Database { get; }
        public string Container { get; }
        public int Timeout { get; }
    }

    public class CosmosChangeFeedConfig
    {
        public CosmosChangeFeedConfig(string consumerGroupName,
            CosmosConfig cosmosConfig)
        {
            ConsumerGroupName = consumerGroupName;
            LeaseContainerSuffix = "-aux";
            MaxReadAhead = 64;
            MaxWriters = 1024;
            Cosmos = cosmosConfig;
        }
        public CosmosChangeFeedConfig(string consumerGroupName, int maxDocuments,
            CosmosConfig cosmosConfig)
        {
            ConsumerGroupName = consumerGroupName;
            LeaseContainerSuffix = "-aux";
            MaxDocuments = maxDocuments;
            MaxReadAhead = 64;
            MaxWriters = 1024;
            Cosmos = cosmosConfig;
        }
        public CosmosChangeFeedConfig(string consumerGroupName, string leaseContainerSuffix,
            CosmosConfig cosmosConfig)
        {
            ConsumerGroupName = consumerGroupName;
            LeaseContainerSuffix = leaseContainerSuffix ?? "-aux";
            MaxReadAhead = 64;
            MaxWriters = 1024;
            Cosmos = cosmosConfig;
        }
        public CosmosChangeFeedConfig(string consumerGroupName, 
            string leaseContainerSuffix, int maxDocuments, int? maxReadAhead, int? maxWriters, float? lagFreqM,
            CosmosConfig cosmosConfig)
        {
            ConsumerGroupName = consumerGroupName;
            LeaseContainerSuffix = leaseContainerSuffix ?? "-aux";
            MaxDocuments = maxDocuments;
            MaxReadAhead = maxReadAhead ?? 64;
            MaxWriters = maxWriters ?? 1024;
            LagFreqM = lagFreqM;
            Cosmos = cosmosConfig;
        }


        public string ConsumerGroupName { get; }
        public string LeaseContainerSuffix { get; }
        public int? MaxDocuments { get; }
        public int MaxReadAhead { get; }
        public int MaxWriters { get; }
        public float? LagFreqM { get; }
        public CosmosConfig Cosmos { get; }
        public string AuxContainerName
        {
            get { return Cosmos.Container + LeaseContainerSuffix; }
        }
    }

    public class CosmosContext : PropulsionContext
    {
        private readonly Discovery _discovery;
        private readonly Connector _connector;
        private readonly ContainerId _source;
        private readonly ContainerId _aux;
        private readonly string _leaseId;
        private readonly int? _maxDocuments;
        
        private readonly float? _lagFrequencyMinutes;
        private bool _startFromTail;

        public CosmosContext(CosmosChangeFeedConfig config)
        {
            var retriesOn429Throttling = 1; // Number of retries before failing processing when provisioned RU/s limit in CosmosDb is breached
            var timeout = TimeSpan.FromSeconds(config.Cosmos.Timeout); // Timeout applied per request to CosmosDb, including retry attempts
            _discovery = Discovery.FromConnectionString(config.Cosmos.ConnectionStringWithUriAndKey);
            _connector = new Connector(timeout, retriesOn429Throttling, 5, Log.Logger, mode: config.Cosmos.Mode );
            _source = new ContainerId(config.Cosmos.Database, config.Cosmos.Container);
            _aux = new ContainerId(config.Cosmos.Database, config.AuxContainerName);
            _leaseId = config.ConsumerGroupName;
            _maxDocuments = config.MaxDocuments;
            MaxReadAhead = config.MaxReadAhead;
            MaxConcurrentStreams = config.MaxWriters;
            _lagFrequencyMinutes = config.LagFreqM;
        }
        public Task StartPipeline()
        {
            projector = StartProjector();
            FSharpFunc<Unit, IChangeFeedObserver> createObserver = new createObserver(projector);
            FSharpOption<int> maxDocuments = ConfigureMaxDocuments();
            FSharpOption<TimeSpan> lagFrequency = ConfigureLagFrequency();


            FSharpAsync<Unit> pipeline = CosmosSource.Run(
                Log.Logger, _discovery, _connector.ClientOptions, _source,
                _aux, _leaseId, _startFromTail, createObserver, maxDocuments: maxDocuments, lagReportFreq: lagFrequency, null);
            

            FSharpAsync.Start(pipeline, null);

            return Task.CompletedTask;
        }

        private FSharpOption<int> ConfigureMaxDocuments()
        {
            if (_maxDocuments.HasValue)
            {
                Log.Information("Processing {leaseId} in {auxContainerName} with max {changeFeedMaxDocuments} documents (<= {maxPending} pending) using {dop} processors", _leaseId, _aux.container, _maxDocuments.Value, MaxReadAhead, MaxConcurrentStreams);
                return FSharpOption<int>.Some(_maxDocuments.Value);
            }

            Log.Information("Processing {leaseId} in {auxContainerName} without document count limit (<= {maxPending} pending) using {dop} processors", _leaseId, _aux.container, MaxReadAhead, MaxConcurrentStreams);
            return FSharpOption<int>.None;
        }

        private FSharpOption<TimeSpan> ConfigureLagFrequency()
        {
            if (_lagFrequencyMinutes.HasValue)
            {
                return FSharpOption<TimeSpan>.Some(TimeSpan.FromMinutes(_lagFrequencyMinutes.Value));
            }

            return FSharpOption<TimeSpan>.None;
        }

        [Serializable]
        internal sealed class createObserver : FSharpFunc<Unit, IChangeFeedObserver>
        {
            public ProjectorPipeline<Ingestion.Ingester<IEnumerable<StreamEvent<byte[]>>, Submission.SubmissionBatch<StreamEvent<byte[]>>>> projector;

            internal createObserver(ProjectorPipeline<Ingestion.Ingester<IEnumerable<StreamEvent<byte[]>>, Submission.SubmissionBatch<StreamEvent<byte[]>>>> projector)
            {
                this.projector = projector;
            }

            public override IChangeFeedObserver Invoke(Unit unitVar0)
            {
                ILogger logger = Log.Logger;
                ProjectorPipeline<Ingestion.Ingester<IEnumerable<StreamEvent<byte[]>>, Submission.SubmissionBatch<StreamEvent<byte[]>>>> objectArg = projector;
                return CosmosSource.CreateObserver(logger, new createInjester(projector), new createMapper());
            }
        }

        [Serializable]
        internal sealed class createInjester : FSharpFunc<Tuple<ILogger, int>, Ingestion.Ingester<
            IEnumerable<StreamEvent<byte[]>>, Submission.SubmissionBatch<StreamEvent<byte[]>>>>
        {
            private readonly ProjectorPipeline<Ingestion.Ingester<IEnumerable<StreamEvent<byte[]>>,
                Submission.SubmissionBatch<StreamEvent<byte[]>>>> objectArg;
            internal createInjester(ProjectorPipeline<Ingestion.Ingester<IEnumerable<StreamEvent<byte[]>>, Submission.SubmissionBatch<StreamEvent<byte[]>>>> objectArg)
            {
                this.objectArg = objectArg;
            }

        public override Ingestion.Ingester<IEnumerable<StreamEvent<byte[]>>, Submission.SubmissionBatch<StreamEvent<byte[]>>> Invoke(Tuple<ILogger, int> tupledArg)
            {
                ILogger item = tupledArg.Item1;
                int item2 = tupledArg.Item2;
                return objectArg.StartIngester(item, item2);
            }
        }

        [Serializable]
        internal sealed class createMapper : FSharpFunc<IReadOnlyList<Document>, IEnumerable<StreamEvent<byte[]>>>
        {
            public override IEnumerable<StreamEvent<byte[]>> Invoke(IReadOnlyList<Document> docs)
            {
                return mapToStreamItems(docs);
            }
        }

        public static IEnumerable<StreamEvent<byte[]>> mapToStreamItems(IEnumerable<Document> docs)
        {
            return SeqModule.Collect<Document, IEnumerable<StreamEvent<byte[]>>, StreamEvent<byte[]>>(new MapToStreamItem(), docs);
        }
        [Serializable]
        internal sealed class MapToStreamItem : FSharpFunc<Document, IEnumerable<StreamEvent<byte[]>>>
        {
            public override IEnumerable<StreamEvent<byte[]>> Invoke(Document d)
            {
                return EquinoxCosmosParser.enumStreamEvents(d);
            }
        }
    }
}
