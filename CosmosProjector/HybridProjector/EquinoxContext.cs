using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.FSharp.Core;
using Newtonsoft.Json;

namespace HybridProjector
{
    class EquinoxContext
    {
    }
    public static class EquinoxCodec
    {
        public static FsCodec.IUnionEncoder<TEvent, byte[]> Create<TEvent>(
            Func<TEvent, Tuple<string, byte[]>> encode,
            Func<string, byte[], TEvent> tryDecode,
            JsonSerializerSettings settings = null) where TEvent : class
        {
            return FsCodec.Codec.Create<TEvent>(
                FuncConvert.FromFunc(encode),
                FuncConvert.FromFunc((Func<Tuple<string, byte[]>, FSharpOption<TEvent>>)TryDecodeImpl));
            FSharpOption<TEvent> TryDecodeImpl(Tuple<string, byte[]> encoded) => OptionModule.OfObj(tryDecode(encoded.Item1, encoded.Item2));
        }

        public static FsCodec.IUnionEncoder<TEvent, byte[]> Create<TEvent>(JsonSerializerSettings settings = null) where TEvent : UnionContract.IUnionContract =>
            FsCodec.NewtonsoftJson.Codec.Create<TEvent>(settings);
    }
}
