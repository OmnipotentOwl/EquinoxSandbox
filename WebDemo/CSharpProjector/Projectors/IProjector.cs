using System.Threading.Tasks;
using Propulsion.Streams;

namespace CSharpProjector.Projectors
{
    public interface IProjector
    {
        Task Project(string _stream, StreamSpan<byte[]> span);
    }
}