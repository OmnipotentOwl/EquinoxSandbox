using Equinox;
using Microsoft.FSharp.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TypeShape;

namespace WebDemo
{
    public abstract class EquinoxContext
    {
        public abstract Func<Target,Equinox.Core.IStream<TEvent, TState>> Resolve<TEvent, TState>(
            FsCodec.IUnionEncoder<TEvent, byte[], object> codec,
            Func<TState, IEnumerable<TEvent>, TState> fold,
            TState initial,
            Func<TEvent, bool> isOrigin = null,
            Func<TState, TEvent> compact = null);

        internal abstract Task Connect();
    }

    public static class EquinoxCodec
    {
        public static FsCodec.IUnionEncoder<TEvent, byte[], object> Create<TEvent>(
            Func<TEvent, Tuple<string,byte[]>> encode,
            Func<string, byte[], TEvent> tryDecode,
            JsonSerializerSettings settings = null) where TEvent: class
        {
            return FsCodec.Codec.Create<TEvent>(
                FuncConvert.FromFunc(encode),
                FuncConvert.FromFunc((Func<Tuple<string, byte[]>, FSharpOption<TEvent>>) TryDecodeImpl));
            FSharpOption<TEvent> TryDecodeImpl(Tuple<string, byte[]> encoded) => OptionModule.OfObj(tryDecode(encoded.Item1, encoded.Item2));
        }

        public static FsCodec.IUnionEncoder<TEvent, byte[], object> Create<TEvent>(JsonSerializerSettings settings = null) where TEvent: UnionContract.IUnionContract =>
            FsCodec.NewtonsoftJson.Codec.Create<TEvent>(settings);
    } 
}