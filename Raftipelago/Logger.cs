using System.Diagnostics;
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

        private static LogLevel currentLogLevel = LogLevel.WARN;
        private static StackLevel currentStackLevel = StackLevel.NONE;

        public static void SetLogLevel(LogLevel logLevel)
        {
            currentLogLevel = logLevel;
        }

        public static void SetStackLevel(StackLevel stackLevel)
        {
            currentStackLevel = stackLevel;
        }

        public static void Trace(object message)
        {
            if (currentLogLevel <= LogLevel.TRACE)
            {
                UnityEngine.Debug.Log(_getLogString(message, "TRC"));
            }
        }

        public static void Debug(object message)
        {
            if (currentLogLevel <= LogLevel.DEBUG)
            {
                UnityEngine.Debug.Log(_getLogString(message, "DBG"));
            }
        }

        public static void Info(object message)
        {
            if (currentLogLevel <= LogLevel.INFO)
            {
                UnityEngine.Debug.Log(_getLogString(message, "IFO"));
            }
        }

        public static void Warn(object message)
        {
            if (currentLogLevel <= LogLevel.WARN)
            {
                UnityEngine.Debug.LogWarning(_getLogString(message, "WRN"));
            }
        }

        public static void Error(object message)
        {
            if (currentLogLevel <= LogLevel.ERROR)
            {
                UnityEngine.Debug.LogError(_getLogString(message, "ERR"));
            }
        }

        public static void Fatal(object message)
        {
            if (currentLogLevel <= LogLevel.FATAL)
            {
                UnityEngine.Debug.LogError(_getLogString(message, "FTL"));
            }
        }

        private static string _getLogString(object message, string level)
        {
            if (currentStackLevel == StackLevel.FULL)
            {
                return $"{level}: {_getFullExternalStack()}: {message}";
            }
            if (currentStackLevel == StackLevel.RAFTIPELAGO)
            {
                return $"{level}: {_getRaftipelagoStack()}: {message}";
            }
            else if (currentStackLevel == StackLevel.SHALLOW)
            {
                return $"{level}: {_getCallingMethod()}: {message}";
            }
            else if (currentStackLevel == StackLevel.LOGLEVEL)
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
