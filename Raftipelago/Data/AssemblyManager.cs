using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Raftipelago.Data
{
    public class AssemblyManager
    {
        public const string ArchipelagoProxyAssembly = "ArchipelagoProxy.dll";
        public const string RaftipelagoTypesAssembly = "RaftipelagoTypes.dll";
        private readonly string[] LibraryFileNames = new string[] { RaftipelagoTypesAssembly, "websocket-sharp.dll", "Archipelago.MultiClient.Net.dll", ArchipelagoProxyAssembly };
        private Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>();

        public AssemblyManager(string fromFolderPath, string toFolderPath)
        {
            if (!Directory.Exists(toFolderPath))
            {
                Directory.CreateDirectory(toFolderPath);
            }
            foreach (var fileName in LibraryFileNames)
            {
                var outputFilePath = Path.Combine(toFolderPath, fileName);
                _copyDllIfNecessary(fromFolderPath, fileName, outputFilePath);
                _loadedAssemblies[fileName] = Assembly.LoadFrom(outputFilePath);
            }
        }

        public Assembly GetAssembly(string assemblyName)
        {
            if (_loadedAssemblies.TryGetValue(assemblyName, out Assembly val))
            {
                return val;
            }
            else
            {
                return null;
            }
        }

        private void _copyDllIfNecessary(string fromFolderPath, string fileName, string outputFilePath)
        {
            // Note to dev: ReadRawFile() will print out a ModManager error in console. This is fine if it only
            // happens once (and is expeted to when loading locally), but if it happens twice for the same file
            // then something's wrong.
            try
            {
                var assemblyData = ComponentManager<EmbeddedFileUtils>.Value.ReadRawFile(fromFolderPath, fileName);
                if (assemblyData.Length > 0)
                {
                    File.WriteAllBytes(outputFilePath, assemblyData);
                }
                else
                {
                    Debug.LogWarning($"File {fileName} not properly read. This may indicate mod packaging/programming issues.");
                }
            }
            catch (Exception)
            {
                Debug.LogWarning($"Unable to copy {fileName}. This can be safely ignored if no errors occur.");
            }
        }
    }
}
