using System;
using System.IO;
using System.Text;

namespace Raftipelago.Data
{
    public class EmbeddedFileUtils
    {
        private readonly Func<string, byte[]> _readEmbeddedResource;
        public EmbeddedFileUtils(Func<string, byte[]> readEmbeddedResource)
        {
            _readEmbeddedResource = readEmbeddedResource;
        }
        public string ReadTextFile(params string[] path)
        {
            var bytes = ReadRawFile(path);
            return Encoding.UTF8.GetString(bytes);
        }
        public byte[] ReadRawFile(params string[] path)
        {
            Logger.Trace("Reading raw file " + string.Join("/", path));
            return _readEmbeddedResource(string.Join("/", path)) ?? _readEmbeddedResource(string.Join("\\", path));
        }
    }
}
