using System;
using System.Threading.Tasks;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using Propulsion.Streams;
using Serilog;

namespace HybridProjector
{
    public static class HybridProjector
    {
        public static FSharpAsync<Unit> Test(StreamSpan<byte[]> stream)
        {
            Log.Debug("Got this:{0}",stream.ToString());

            foreach (var streamEvent in stream.events)
            {
                FsCodec.Codec.
            }








            return FSharpAsync.Sleep(1);
        }
    }
}
