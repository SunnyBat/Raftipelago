using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Raftipelago
{
    /// <summary>
    /// Very simple logger. Will log varying levels of events as necessary. Can also log specific assembly stacks.
    /// </summary>
    public class Logger
    {
        private static readonly string[] KnownRaftipelagoAssemblies = new string[] { "RaftipelagoTypes", "ArchipelagoProxy", Assembly.GetExecutingAssembly().GetName().Name };

        private static LogLevel _currentLogLevel = LogLevel.TRACE;
        private static StackLevel _currentStackLevel = StackLevel.NONE;
        private static FileStream _currentOutputFileStream;

        public static void SetLogLevel(LogLevel logLevel)
        {
            _currentLogLevel = logLevel;
        }

        public static void SetStackLevel(StackLevel stackLevel)
        {
            _currentStackLevel = stackLevel;
        }

        public static void Trace(object message)
        {
            if (_currentLogLevel <= LogLevel.TRACE)
            {
                var strToWrite = _getLogString(message, "TRC");
                UnityEngine.Debug.Log(strToWrite);
                _writeToFileStream(strToWrite);
            }
        }

        public static void Debug(object message)
        {
            if (_currentLogLevel <= LogLevel.DEBUG)
            {
                var strToWrite = _getLogString(message, "DBG");
                UnityEngine.Debug.Log(strToWrite);
                _writeToFileStream(strToWrite);
            }
        }

        public static void Info(object message)
        {
            if (_currentLogLevel <= LogLevel.INFO)
            {
                var strToWrite = _getLogString(message, "IFO");
                UnityEngine.Debug.Log(strToWrite);
                _writeToFileStream(strToWrite);
            }
        }

        public static void Warn(object message)
        {
            if (_currentLogLevel <= LogLevel.WARN)
            {
                var strToWrite = _getLogString(message, "WRN");
                UnityEngine.Debug.LogWarning(strToWrite);
                _writeToFileStream(strToWrite);
            }
        }

        public static void Error(object message)
        {
            if (_currentLogLevel <= LogLevel.ERROR)
            {
                var strToWrite = _getLogString(message, "ERR");
                UnityEngine.Debug.LogError(strToWrite);
                _writeToFileStream(strToWrite);
            }
        }

        public static void Fatal(object message)
        {
            if (_currentLogLevel <= LogLevel.FATAL)
            {
                var strToWrite = _getLogString(message, "FTL");
                UnityEngine.Debug.LogError(strToWrite);
                _writeToFileStream(strToWrite);
            }
        }

        public static void SetLogFile(string filePath)
        {
            CloseLogFile();
            if (!File.Exists(filePath))
            {
                UnityEngine.Debug.Log("Creating log file: " + filePath);
                File.Create(filePath).Close();
            }

            UnityEngine.Debug.Log("Opening log file: " + filePath);
            _currentOutputFileStream = File.Open(filePath, FileMode.Append, FileAccess.Write);
            UnityEngine.Debug.Log("File opened");
        }

        public static void CloseLogFile()
        {
            if (_currentOutputFileStream != null)
            {
                try
                {
                    _currentOutputFileStream.Flush();
                    _currentOutputFileStream.Dispose();
                    _currentOutputFileStream = null;
                }
                catch
                {
                }
            }
        }

        private static string _getLogString(object message, string level)
        {
            if (_currentStackLevel == StackLevel.FULL)
            {
                return $"{level}: {_getFullExternalStack()}: {message}";
            }
            if (_currentStackLevel == StackLevel.RAFTIPELAGO)
            {
                return $"{level}: {_getRaftipelagoStack()}: {message}";
            }
            else if (_currentStackLevel == StackLevel.SHALLOW)
            {
                return $"{level}: {_getCallingMethod()}: {message}";
            }
            else if (_currentStackLevel == StackLevel.LOGLEVEL)
            {
                return $"{level}: {message}";
            }
            else
            {
                return $"{message}";
            }
        }

        private static string _getCallingMethod()
        {
            var stackFrames = new StackTrace().GetFrames();
            if (stackFrames.Length > 3)
            {
                return $"{stackFrames[3].GetMethod()}";
            }
            else
            {
                return "<Unknown>.<Unknown>";
            }
        }

        private static string _getRaftipelagoStack()
        {
            StringBuilder sb = new StringBuilder();
            var stackFrames = new StackTrace().GetFrames();
            if (stackFrames.Length > 3)
            {
                foreach (var frame in stackFrames.Skip(3))
                {
                    var assemblyName = frame.GetMethod().ReflectedType.Assembly.GetName().Name;
                    if (KnownRaftipelagoAssemblies.Any(knownAssembly => assemblyName == knownAssembly))
                    {
                        if (sb.Length == 0)
                        {
                            sb.Append("\n");
                        }
                        sb.Append("\t");
                        sb.Append(frame.GetMethod().ReflectedType.FullName);
                        sb.Append("::");
                        sb.Append(frame.GetMethod());
                        sb.Append("\n");
                    }
                }
                return sb.ToString();
            }
            else
            {
                return "<Unknown>.<Unknown>";
            }
        }

        private static string _getFullExternalStack()
        {
            StringBuilder sb = new StringBuilder();
            var stackFrames = new StackTrace().GetFrames();
            if (stackFrames.Length > 3)
            {
                foreach (var frame in stackFrames.Skip(3))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append("\t");
                    }
                    else
                    {
                        sb.Append("\n");
                    }
                    sb.Append(frame.GetMethod().ReflectedType.FullName);
                    sb.Append("::");
                    sb.Append(frame.GetMethod());
                    sb.Append("\n");
                }
                return sb.ToString();
            }
            else
            {
                return "<Unknown>.<Unknown>";
            }
        }

        private static void _writeToFileStream(string output)
        {
            if (output != null && _currentOutputFileStream != null)
            {
                var outputBytes = Encoding.UTF8.GetBytes(output);
                _currentOutputFileStream.Write(outputBytes, 0, outputBytes.Length);
                var newLineBytes = Encoding.UTF8.GetBytes("\r\n");
                _currentOutputFileStream.Write(newLineBytes, 0, newLineBytes.Length);
            }
        }

        public enum LogLevel
        {
            TRACE = 1,
            DEBUG = 2,
            INFO = 3,
            WARN = 4,
            ERROR = 5,
            FATAL = 6
        }

        public enum StackLevel
        {
            FULL = 1,
            RAFTIPELAGO = 2,
            SHALLOW = 3,
            LOGLEVEL = 4,
            NONE = 5
        }
    }
}
