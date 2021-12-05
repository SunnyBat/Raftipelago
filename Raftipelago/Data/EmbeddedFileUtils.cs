using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Raftipelago.Data
{
    public class EmbeddedFileUtils
    {
        public static string ReadFile(params string[] path)
        {
            // https://stackoverflow.com/a/9486411
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            var fullPath = new List<string>();
            fullPath.Add(assemblyDirectory);
            fullPath.AddRange(path);
            string textPath = Path.Combine(fullPath.ToArray());
            return File.ReadAllText(textPath);
        }
    }
}
