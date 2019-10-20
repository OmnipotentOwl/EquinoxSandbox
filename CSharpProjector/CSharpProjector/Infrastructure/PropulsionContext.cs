using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpProjector.Projectors;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using Propulsion;
using Propulsion.Streams;
using Serilog;

namespace CSharpProjector.Infrastructure
{
    public class PropulsionContext
    {
        public ProjectorPipeline<Ingestion.Ingester<IEnumerable<StreamEvent<byte[]>>, Submission.SubmissionBatch<StreamEvent<byte[]>>>> projector;
        public IProjector ProjectorHandler;

        public int MaxReadAhead;
        public int MaxConcurrentStreams;
        public PropulsionContext()
        {
        }
        public ProjectorPipeline<Ingestion.Ingester<IEnumerable<StreamEvent<byte[]>>, Submission.SubmissionBatch<StreamEvent<byte[]>>>> StartProjector()
        {
            FSharpFunc<string, string> categorize = new categorize();
            FSharpFunc<Tuple<string, StreamSpan<byte[]>>, FSharpAsync<Unit>> project = new projectBuilder(ProjectorHandler);
            return StreamsProjector.Start(
                Log.Logger, MaxReadAhead, MaxConcurrentStreams, project, categorize, FSharpOption<TimeSpan>.Some(TimeSpan.FromMinutes(1.0)), FSharpOption<TimeSpan>.Some(TimeSpan.FromMinutes(5.0)));
        }

        public Task RunProjector(CancellationToken stoppingToken)
        {
            FSharpAsync<Unit> computation2 = projector.AwaitCompletion();
            FSharpAsync.RunSynchronously(computation2, null, stoppingToken);
            return Task.CompletedTask;
        }

        internal sealed class categorize : FSharpFunc<string, string>
        {
            public override string Invoke(string streamName)
            {
                return streamName.Split(new char[1]
                {
                    '-'
                }, 2, StringSplitOptions.RemoveEmptyEntries)[0];
            }
        }

        [Serializable]
        internal sealed class projectBuilder : FSharpFunc<Tuple<string, StreamSpan<byte[]>>, FSharpAsync<Unit>>
        {
            private readonly IProjector _projectorHandler;
            internal projectBuilder(IProjector projectorHandler)
            {
                _projectorHandler = projectorHandler;
            }
            public override FSharpAsync<Unit> Invoke(Tuple<string, StreamSpan<byte[]>> tupledArg)
            {
                string _stream = tupledArg.Item1;
                StreamSpan<byte[]> span = tupledArg.Item2;
                FSharpAsyncBuilder defaultAsyncBuilder = ExtraTopLevelOperators.DefaultAsyncBuilder;
                return defaultAsyncBuilder.Delay(new projectInvoke(span, _stream, defaultAsyncBuilder, _projectorHandler));
            }
        }

        [Serializable]
        internal sealed class projectInvoke : FSharpFunc<Unit, FSharpAsync<Unit>>
        {
            private readonly IProjector _projectorHandler;
            internal projectInvoke(StreamSpan<byte[]> span, string _stream, FSharpAsyncBuilder builder, IProjector projectorHandler)
            {
                this.span = span;
                this._stream = _stream;
                this.builder = builder;
                _projectorHandler = projectorHandler;
            }
            public StreamSpan<byte[]> span;
            public string _stream;
            public FSharpAsyncBuilder builder;
            public override FSharpAsync<Unit> Invoke(Unit unitVar)
            {
                _ = builder;
                return FSharpAsync.AwaitTask(_projectorHandler.Project(_stream, span));
            }
        }

    }
}
