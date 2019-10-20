using Propulsion.Streams;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using WebDemo;

namespace CSharpProjector.Projectors
{
    public class SummaryProjector : IProjector
    {
        public Task Project(string _stream, StreamSpan<byte[]> span)
        {
            Log.Debug("Got this:{0}", span.ToString());
            var name = _stream.Split('-', 2, StringSplitOptions.RemoveEmptyEntries)[0];
            var id = _stream.Split('-', 2, StringSplitOptions.RemoveEmptyEntries)[1];

            switch (name)
            {
                case "Todos":
                    ProcessTodoStream(id, span);
                    break;

                default:
                    throw new NotImplementedException();
            }

            return Task.CompletedTask;
        }

        private void ProcessTodoStream(string streamId, StreamSpan<byte[]> streamData)
        {
            foreach (var streamEvent in streamData.events)
            {

                var processedEvent = Todo.Event.TryDecode(streamEvent.EventType, streamEvent.Data);

                Log.Debug("Processed Event: {Event}", processedEvent);

            }
        }
    }
}
