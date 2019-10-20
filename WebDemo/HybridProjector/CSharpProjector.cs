using System;
using System.Threading.Tasks;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using Propulsion.Streams;
using Serilog;
using WebDemo;

namespace HybridProjector
{
    public static class CSharpProjector
    {
        public static Task Test(string streamName,StreamSpan<byte[]> streamData)
        {
            Log.Debug("Got this:{0}", streamData.ToString());

            var name = streamName.Split(new char['-'], 2, StringSplitOptions.RemoveEmptyEntries)[0];
            var id = streamName.Split(new char['-'], 2, StringSplitOptions.RemoveEmptyEntries)[1];
            switch (name)
            {
                case nameof(Todo):
                    ProcessTodoStream(id,streamData);
                    break;
            }

            

            return Task.CompletedTask;
        }

        private static void ProcessTodoStream(string streamId,StreamSpan<byte[]> streamData)
        {
            foreach (var streamEvent in streamData.events)
            {

                var processedEvent = Todo.Event.TryDecode(streamEvent.EventType, streamEvent.Data);


            }
        }
    }
}
