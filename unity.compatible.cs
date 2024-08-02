// See https://aka.ms/new-console-template for more information
using System;
using System.Diagnostics.Contracts;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Runtime.Serialization;
using System.ComponentModel;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Unity.Collections.LowLevel.Unsafe;
using System.Text;
using System.Collections;
using System.Globalization;
using System.Data;

namespace UnityEngine
{
    public enum LogType
    {
        //
        // 摘要:
        //     LogType used for Errors.
        Error,
        //
        // 摘要:
        //     LogType used for Asserts. (These could also indicate an error inside Unity itself.)
        Assert,
        //
        // 摘要:
        //     LogType used for Warnings.
        Warning,
        //
        // 摘要:
        //     LogType used for regular log messages.
        Log,
        //
        // 摘要:
        //     LogType used for Exceptions.
        Exception
    }

    public interface ILogHandler
    {
        //
        // 摘要:
        //     Logs a formatted message.
        //
        // 参数:
        //   logType:
        //     The type of the log message.
        //
        //   context:
        //     Object to which the message applies.
        //
        //   format:
        //     A composite format string.
        //
        //   args:
        //     Format arguments.
        void LogFormat(LogType logType, Object context, string format, params object[] args);

        //
        // 摘要:
        //     A variant of ILogHandler.LogFormat that logs an exception message.
        //
        // 参数:
        //   exception:
        //     Runtime Exception.
        //
        //   context:
        //     Object to which the message applies.
        void LogException(Exception exception, Object context);
    }
    public interface ILogger : ILogHandler
    {
        //
        // 摘要:
        //     Set Logger.ILogHandler.
        ILogHandler logHandler { get; set; }

        //
        // 摘要:
        //     To runtime toggle debug logging [ON/OFF].
        bool logEnabled { get; set; }

        //
        // 摘要:
        //     To selective enable debug log message.
        LogType filterLogType { get; set; }

        //
        // 摘要:
        //     Check logging is enabled based on the LogType.
        //
        // 参数:
        //   logType:
        //
        // 返回结果:
        //     Retrun true in case logs of LogType will be logged otherwise returns false.
        bool IsLogTypeAllowed(LogType logType);

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //
        //   message:
        //
        //   context:
        //
        //   tag:
        void Log(LogType logType, object message);

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //
        //   message:
        //
        //   context:
        //
        //   tag:
        void Log(LogType logType, object message, Object context);

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //
        //   message:
        //
        //   context:
        //
        //   tag:
        void Log(LogType logType, string tag, object message);

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //
        //   message:
        //
        //   context:
        //
        //   tag:
        void Log(LogType logType, string tag, object message, Object context);

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //
        //   message:
        //
        //   context:
        //
        //   tag:
        void Log(object message);

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //
        //   message:
        //
        //   context:
        //
        //   tag:
        void Log(string tag, object message);

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //
        //   message:
        //
        //   context:
        //
        //   tag:
        void Log(string tag, object message, Object context);

        //
        // 摘要:
        //     A variant of Logger.Log that logs an warning message.
        //
        // 参数:
        //   tag:
        //
        //   message:
        //
        //   context:
        void LogWarning(string tag, object message);

        //
        // 摘要:
        //     A variant of Logger.Log that logs an warning message.
        //
        // 参数:
        //   tag:
        //
        //   message:
        //
        //   context:
        void LogWarning(string tag, object message, Object context);

        //
        // 摘要:
        //     A variant of ILogger.Log that logs an error message.
        //
        // 参数:
        //   tag:
        //
        //   message:
        //
        //   context:
        void LogError(string tag, object message);

        //
        // 摘要:
        //     A variant of ILogger.Log that logs an error message.
        //
        // 参数:
        //   tag:
        //
        //   message:
        //
        //   context:
        void LogError(string tag, object message, Object context);

        //
        // 摘要:
        //     Logs a formatted message.
        //
        // 参数:
        //   logType:
        //
        //   format:
        //
        //   args:
        void LogFormat(LogType logType, string format, params object[] args);

        //
        // 摘要:
        //     A variant of ILogger.Log that logs an exception message.
        //
        // 参数:
        //   exception:
        void LogException(Exception exception);
    }

    public class Logger : ILogger, ILogHandler
    {
        private const string kNoTagFormat = "{0}";

        private const string kTagFormat = "{0}: {1}";

        //
        // 摘要:
        //     Set Logger.ILogHandler.
        public ILogHandler logHandler { get; set; }

        //
        // 摘要:
        //     To runtime toggle debug logging [ON/OFF].
        public bool logEnabled { get; set; }

        //
        // 摘要:
        //     To selective enable debug log message.
        public LogType filterLogType { get; set; }

        private Logger()
        {
        }

        //
        // 摘要:
        //     Create a custom Logger.
        //
        // 参数:
        //   logHandler:
        //     Pass in default log handler or custom log handler.
        public Logger(ILogHandler logHandler)
        {
            this.logHandler = logHandler;
            logEnabled = true;
            filterLogType = LogType.Log;
        }

        //
        // 摘要:
        //     Check logging is enabled based on the LogType.
        //
        // 参数:
        //   logType:
        //     The type of the log message.
        //
        // 返回结果:
        //     Retrun true in case logs of LogType will be logged otherwise returns false.
        public bool IsLogTypeAllowed(LogType logType)
        {
            if (logEnabled)
            {
                if (logType == LogType.Exception)
                {
                    return true;
                }

                if (filterLogType != LogType.Exception)
                {
                    return logType <= filterLogType;
                }
            }

            return false;
        }

        private static string GetString(object message)
        {
            if (message == null)
            {
                return "Null";
            }

            IFormattable formattable = message as IFormattable;
            if (formattable != null)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return message.ToString();
        }

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //     The type of the log message.
        //
        //   tag:
        //     Used to identify the source of a log message. It usually identifies the class
        //     where the log call occurs.
        //
        //   message:
        //     String or object to be converted to string representation for display.
        //
        //   context:
        //     Object to which the message applies.
        public void Log(LogType logType, object message)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, null, "{0}", GetString(message));
            }
        }

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //     The type of the log message.
        //
        //   tag:
        //     Used to identify the source of a log message. It usually identifies the class
        //     where the log call occurs.
        //
        //   message:
        //     String or object to be converted to string representation for display.
        //
        //   context:
        //     Object to which the message applies.
        public void Log(LogType logType, object message, Object context)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, context, "{0}", GetString(message));
            }
        }

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //     The type of the log message.
        //
        //   tag:
        //     Used to identify the source of a log message. It usually identifies the class
        //     where the log call occurs.
        //
        //   message:
        //     String or object to be converted to string representation for display.
        //
        //   context:
        //     Object to which the message applies.
        public void Log(LogType logType, string tag, object message)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, null, "{0}: {1}", tag, GetString(message));
            }
        }

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //     The type of the log message.
        //
        //   tag:
        //     Used to identify the source of a log message. It usually identifies the class
        //     where the log call occurs.
        //
        //   message:
        //     String or object to be converted to string representation for display.
        //
        //   context:
        //     Object to which the message applies.
        public void Log(LogType logType, string tag, object message, Object context)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, context, "{0}: {1}", tag, GetString(message));
            }
        }

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //     The type of the log message.
        //
        //   tag:
        //     Used to identify the source of a log message. It usually identifies the class
        //     where the log call occurs.
        //
        //   message:
        //     String or object to be converted to string representation for display.
        //
        //   context:
        //     Object to which the message applies.
        public void Log(object message)
        {
            if (IsLogTypeAllowed(LogType.Log))
            {
                logHandler.LogFormat(LogType.Log, null, "{0}", GetString(message));
            }
        }

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //     The type of the log message.
        //
        //   tag:
        //     Used to identify the source of a log message. It usually identifies the class
        //     where the log call occurs.
        //
        //   message:
        //     String or object to be converted to string representation for display.
        //
        //   context:
        //     Object to which the message applies.
        public void Log(string tag, object message)
        {
            if (IsLogTypeAllowed(LogType.Log))
            {
                logHandler.LogFormat(LogType.Log, null, "{0}: {1}", tag, GetString(message));
            }
        }

        //
        // 摘要:
        //     Logs message to the Unity Console using default logger.
        //
        // 参数:
        //   logType:
        //     The type of the log message.
        //
        //   tag:
        //     Used to identify the source of a log message. It usually identifies the class
        //     where the log call occurs.
        //
        //   message:
        //     String or object to be converted to string representation for display.
        //
        //   context:
        //     Object to which the message applies.
        public void Log(string tag, object message, Object context)
        {
            if (IsLogTypeAllowed(LogType.Log))
            {
                logHandler.LogFormat(LogType.Log, context, "{0}: {1}", tag, GetString(message));
            }
        }

        //
        // 摘要:
        //     A variant of Logger.Log that logs an warning message.
        //
        // 参数:
        //   tag:
        //     Used to identify the source of a log message. It usually identifies the class
        //     where the log call occurs.
        //
        //   message:
        //     String or object to be converted to string representation for display.
        //
        //   context:
        //     Object to which the message applies.
        public void LogWarning(string tag, object message)
        {
            if (IsLogTypeAllowed(LogType.Warning))
            {
                logHandler.LogFormat(LogType.Warning, null, "{0}: {1}", tag, GetString(message));
            }
        }

        //
        // 摘要:
        //     A variant of Logger.Log that logs an warning message.
        //
        // 参数:
        //   tag:
        //     Used to identify the source of a log message. It usually identifies the class
        //     where the log call occurs.
        //
        //   message:
        //     String or object to be converted to string representation for display.
        //
        //   context:
        //     Object to which the message applies.
        public void LogWarning(string tag, object message, Object context)
        {
            if (IsLogTypeAllowed(LogType.Warning))
            {
                logHandler.LogFormat(LogType.Warning, context, "{0}: {1}", tag, GetString(message));
            }
        }

        //
        // 摘要:
        //     A variant of Logger.Log that logs an error message.
        //
        // 参数:
        //   tag:
        //     Used to identify the source of a log message. It usually identifies the class
        //     where the log call occurs.
        //
        //   message:
        //     String or object to be converted to string representation for display.
        //
        //   context:
        //     Object to which the message applies.
        public void LogError(string tag, object message)
        {
            if (IsLogTypeAllowed(LogType.Error))
            {
                logHandler.LogFormat(LogType.Error, null, "{0}: {1}", tag, GetString(message));
            }
        }

        //
        // 摘要:
        //     A variant of Logger.Log that logs an error message.
        //
        // 参数:
        //   tag:
        //     Used to identify the source of a log message. It usually identifies the class
        //     where the log call occurs.
        //
        //   message:
        //     String or object to be converted to string representation for display.
        //
        //   context:
        //     Object to which the message applies.
        public void LogError(string tag, object message, Object context)
        {
            if (IsLogTypeAllowed(LogType.Error))
            {
                logHandler.LogFormat(LogType.Error, context, "{0}: {1}", tag, GetString(message));
            }
        }

        //
        // 摘要:
        //     A variant of Logger.Log that logs an exception message.
        //
        // 参数:
        //   exception:
        //     Runtime Exception.
        //
        //   context:
        //     Object to which the message applies.
        public void LogException(Exception exception)
        {
            if (logEnabled)
            {
                logHandler.LogException(exception, null);
            }
        }

        //
        // 摘要:
        //     A variant of Logger.Log that logs an exception message.
        //
        // 参数:
        //   exception:
        //     Runtime Exception.
        //
        //   context:
        //     Object to which the message applies.
        public void LogException(Exception exception, Object context)
        {
            if (logEnabled)
            {
                logHandler.LogException(exception, context);
            }
        }

        //
        // 摘要:
        //     Logs a formatted message.
        //
        // 参数:
        //   logType:
        //     The type of the log message.
        //
        //   context:
        //     Object to which the message applies.
        //
        //   format:
        //     A composite format string.
        //
        //   args:
        //     Format arguments.
        public void LogFormat(LogType logType, string format, params object[] args)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, null, format, args);
            }
        }

        //
        // 摘要:
        //     Logs a formatted message.
        //
        // 参数:
        //   logType:
        //     The type of the log message.
        //
        //   context:
        //     Object to which the message applies.
        //
        //   format:
        //     A composite format string.
        //
        //   args:
        //     Format arguments.
        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (IsLogTypeAllowed(logType))
            {
                logHandler.LogFormat(logType, context, format, args);
            }
        }
    }

    public class Debug
    {
        internal static readonly ILogger s_DefaultLogger = new Logger(new DebugLogHandler());

        internal static ILogger s_Logger = new Logger(new DebugLogHandler());
        public static ILogger unityLogger => s_Logger;

        public static void LogErrorFormat(string format, params object[] args)
        {
            unityLogger.LogFormat(LogType.Error, format, args);
        }

        public static void LogError(object message)
        {
            unityLogger.Log(LogType.Error, message);
        }

        [Conditional("UNITY_ASSERTIONS")]
        public static void Assert(bool condition)
        {
            if (!condition)
            {
                unityLogger.Log(LogType.Assert, "Assertion failed");
            }
        }
        public static void LogWarning(object message)
        {
            unityLogger.Log(LogType.Warning, message);
        }

        public static void Log(object message)
        {
            unityLogger.Log(LogType.Log, message);
        }
    }
    public enum LogOption
    {
        //
        // 摘要:
        //     Normal log message.
        None,
        //
        // 摘要:
        //     The log message will not have a stacktrace appended automatically.
        NoStacktrace
    }
    internal sealed class DebugLogHandler : ILogHandler
    {
        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            //Internal_Log(logType, LogOption.None, string.Format(format, args), context);
            System.Diagnostics.Debug.WriteLine(string.Format(format, args));
        }

        public void LogFormat(LogType logType, LogOption logOptions, Object context, string format, params object[] args)
        {
            //Internal_Log(logType, logOptions, string.Format(format, args), context);
            System.Diagnostics.Debug.WriteLine(string.Format(format, args));

        }

        public void LogException(Exception exception, Object context)
        {
            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            //Internal_LogException(exception, context);
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }


    [Serializable]
    public class UnityException : SystemException
    {
        private const int Result = -2147467261;

        private string unityStackTrace;

        public UnityException()
            : base("A Unity Runtime error occurred!")
        {
            base.HResult = -2147467261;
            unityStackTrace = Environment.StackTrace;
        }

        public UnityException(string message)
            : base(message)
        {
            base.HResult = -2147467261;
            unityStackTrace = Environment.StackTrace;
        }

        public UnityException(string message, Exception innerException)
            : base(message, innerException)
        {
            base.HResult = -2147467261;
            unityStackTrace = Environment.StackTrace;
        }

        protected UnityException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            unityStackTrace = Environment.StackTrace;
        }
    }

    public enum RuntimePlatform
    {
        //
        // 摘要:
        //     In the Unity editor on macOS.
        OSXEditor = 0,
        //
        // 摘要:
        //     In the player on macOS.
        OSXPlayer = 1,
        //
        // 摘要:
        //     In the player on Windows.
        WindowsPlayer = 2,
        //
        // 摘要:
        //     In the web player on macOS.
        [Obsolete("WebPlayer export is no longer supported in Unity 5.4+.", true)]
        OSXWebPlayer = 3,
        //
        // 摘要:
        //     In the Dashboard widget on macOS.
        [Obsolete("Dashboard widget on Mac OS X export is no longer supported in Unity 5.4+.", true)]
        OSXDashboardPlayer = 4,
        //
        // 摘要:
        //     In the web player on Windows.
        [Obsolete("WebPlayer export is no longer supported in Unity 5.4+.", true)]
        WindowsWebPlayer = 5,
        //
        // 摘要:
        //     In the Unity editor on Windows.
        WindowsEditor = 7,
        //
        // 摘要:
        //     In the player on the iPhone.
        IPhonePlayer = 8,
        [Obsolete("Xbox360 export is no longer supported in Unity 5.5+.")]
        XBOX360 = 10,
        [Obsolete("PS3 export is no longer supported in Unity >=5.5.")]
        PS3 = 9,
        //
        // 摘要:
        //     In the player on Android devices.
        Android = 11,
        [Obsolete("NaCl export is no longer supported in Unity 5.0+.")]
        NaCl = 12,
        [Obsolete("FlashPlayer export is no longer supported in Unity 5.0+.")]
        FlashPlayer = 0xF,
        //
        // 摘要:
        //     In the player on Linux.
        LinuxPlayer = 13,
        //
        // 摘要:
        //     In the Unity editor on Linux.
        LinuxEditor = 0x10,
        //
        // 摘要:
        //     In the player on WebGL
        WebGLPlayer = 17,
        [Obsolete("Use WSAPlayerX86 instead")]
        MetroPlayerX86 = 18,
        //
        // 摘要:
        //     In the player on Windows Store Apps when CPU architecture is X86.
        WSAPlayerX86 = 18,
        [Obsolete("Use WSAPlayerX64 instead")]
        MetroPlayerX64 = 19,
        //
        // 摘要:
        //     In the player on Windows Store Apps when CPU architecture is X64.
        WSAPlayerX64 = 19,
        [Obsolete("Use WSAPlayerARM instead")]
        MetroPlayerARM = 20,
        //
        // 摘要:
        //     In the player on Windows Store Apps when CPU architecture is ARM.
        WSAPlayerARM = 20,
        [Obsolete("Windows Phone 8 was removed in 5.3")]
        WP8Player = 21,
        [Obsolete("BB10Player export is no longer supported in Unity 5.4+.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        BB10Player = 22,
        [Obsolete("BlackBerryPlayer export is no longer supported in Unity 5.4+.")]
        BlackBerryPlayer = 22,
        [Obsolete("TizenPlayer export is no longer supported in Unity 2017.3+.")]
        TizenPlayer = 23,
        [Obsolete("PSP2 is no longer supported as of Unity 2018.3")]
        PSP2 = 24,
        //
        // 摘要:
        //     In the player on the Playstation 4.
        PS4 = 25,
        [Obsolete("PSM export is no longer supported in Unity >= 5.3")]
        PSM = 26,
        //
        // 摘要:
        //     In the player on Xbox One.
        XboxOne = 27,
        [Obsolete("SamsungTVPlayer export is no longer supported in Unity 2017.3+.")]
        SamsungTVPlayer = 28,
        [Obsolete("Wii U is no longer supported in Unity 2018.1+.")]
        WiiU = 30,
        //
        // 摘要:
        //     In the player on the Apple's tvOS.
        tvOS = 0x1F,
        //
        // 摘要:
        //     In the player on Nintendo Switch.
        Switch = 0x20,
        Lumin = 33,
        //
        // 摘要:
        //     In the player on Stadia.
        Stadia = 34,
        //
        // 摘要:
        //     In the player on CloudRendering.
        CloudRendering = 35,
        [Obsolete("GameCoreScarlett is deprecated, please use GameCoreXboxSeries (UnityUpgradable) -> GameCoreXboxSeries", false)]
        GameCoreScarlett = -1,
        GameCoreXboxSeries = 36,
        GameCoreXboxOne = 37,
        //
        // 摘要:
        //     In the player on the Playstation 5.
        PS5 = 38,
        EmbeddedLinuxArm64 = 39,
        EmbeddedLinuxArm32 = 40,
        EmbeddedLinuxX64 = 41,
        EmbeddedLinuxX86 = 42,
        //
        // 摘要:
        //     In the server on Linux.
        LinuxServer = 43,
        //
        // 摘要:
        //     In the server on Windows.
        WindowsServer = 44,
        //
        // 摘要:
        //     In the server on macOS.
        OSXServer = 45
    }
    public enum GraphicsDeviceType
    {
        //
        // 摘要:
        //     OpenGL 2.x graphics API. (deprecated, only available on Linux and MacOSX)
        [Obsolete("OpenGL2 is no longer supported in Unity 5.5+")]
        OpenGL2 = 0,
        //
        // 摘要:
        //     Direct3D 9 graphics API.
        [Obsolete("Direct3D 9 is no longer supported in Unity 2017.2+")]
        Direct3D9 = 1,
        //
        // 摘要:
        //     Direct3D 11 graphics API.
        Direct3D11 = 2,
        //
        // 摘要:
        //     PlayStation 3 graphics API.
        [Obsolete("PS3 is no longer supported in Unity 5.5+")]
        PlayStation3 = 3,
        //
        // 摘要:
        //     No graphics API.
        Null = 4,
        [Obsolete("Xbox360 is no longer supported in Unity 5.5+")]
        Xbox360 = 6,
        //
        // 摘要:
        //     OpenGL ES 2.0 graphics API. (deprecated on iOS and tvOS)
        OpenGLES2 = 8,
        //
        // 摘要:
        //     OpenGL ES 3.0 graphics API. (deprecated on iOS and tvOS)
        OpenGLES3 = 11,
        [Obsolete("PVita is no longer supported as of Unity 2018")]
        PlayStationVita = 12,
        //
        // 摘要:
        //     PlayStation 4 graphics API.
        PlayStation4 = 13,
        //
        // 摘要:
        //     Xbox One graphics API using Direct3D 11.
        XboxOne = 14,
        //
        // 摘要:
        //     PlayStation Mobile (PSM) graphics API.
        [Obsolete("PlayStationMobile is no longer supported in Unity 5.3+")]
        PlayStationMobile = 0xF,
        //
        // 摘要:
        //     iOS Metal graphics API.
        Metal = 0x10,
        //
        // 摘要:
        //     OpenGL (Core profile - GL3 or later) graphics API.
        OpenGLCore = 17,
        //
        // 摘要:
        //     Direct3D 12 graphics API.
        Direct3D12 = 18,
        //
        // 摘要:
        //     Nintendo 3DS graphics API.
        [Obsolete("Nintendo 3DS support is unavailable since 2018.1")]
        N3DS = 19,
        //
        // 摘要:
        //     Vulkan (EXPERIMENTAL).
        Vulkan = 21,
        //
        // 摘要:
        //     Nintendo Switch graphics API.
        Switch = 22,
        //
        // 摘要:
        //     Xbox One graphics API using Direct3D 12.
        XboxOneD3D12 = 23,
        //
        // 摘要:
        //     Game Core Xbox One graphics API using Direct3D 12.
        GameCoreXboxOne = 24,
        [Obsolete("GameCoreScarlett is deprecated, please use GameCoreXboxSeries (UnityUpgradable) -> GameCoreXboxSeries", false)]
        GameCoreScarlett = -1,
        //
        // 摘要:
        //     Game Core XboxSeries graphics API using Direct3D 12.
        GameCoreXboxSeries = 25,
        PlayStation5 = 26,
        PlayStation5NGGC = 27
    }
    public enum ScriptingImplementation
    {
        //
        // 摘要:
        //     The standard Mono 2.6 runtime.
        Mono2x,
        //
        // 摘要:
        //     Unity's .NET runtime.
        IL2CPP,
        //
        // 摘要:
        //     Microsoft's .NET runtime.
        WinRTDotNET
    }
    [Flags]
    public enum HideFlags
    {
        //
        // 摘要:
        //     A normal, visible object. This is the default.
        None = 0x0,
        //
        // 摘要:
        //     The object will not appear in the hierarchy.
        HideInHierarchy = 0x1,
        //
        // 摘要:
        //     It is not possible to view it in the inspector.
        HideInInspector = 0x2,
        //
        // 摘要:
        //     The object will not be saved to the Scene in the editor.
        DontSaveInEditor = 0x4,
        //
        // 摘要:
        //     The object is not editable in the Inspector.
        NotEditable = 0x8,
        //
        // 摘要:
        //     The object will not be saved when building a player.
        DontSaveInBuild = 0x10,
        //
        // 摘要:
        //     The object will not be unloaded by Resources.UnloadUnusedAssets.
        DontUnloadUnusedAsset = 0x20,
        //
        // 摘要:
        //     The object will not be saved to the Scene. It will not be destroyed when a new
        //     Scene is loaded. It is a shortcut for HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
        //     | HideFlags.DontUnloadUnusedAsset.
        DontSave = 0x34,
        //
        // 摘要:
        //     The GameObject is not shown in the Hierarchy, not saved to Scenes, and not unloaded
        //     by Resources.UnloadUnusedAssets.
        HideAndDontSave = 0x3D
    }

    public class Mathf
    {
        public static int FloorToInt(float f)
        {
            return (int)Math.Floor(f);
        }
    }
}

namespace Unity.Profiling
{

    public struct ProfilerMarker
    {
        internal readonly IntPtr m_Ptr;

        //
        // 摘要:
        //     Helper IDisposable struct for use with ProfilerMarker.Auto.
        public struct AutoScope : IDisposable
        {
            internal readonly IntPtr m_Ptr;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal AutoScope(IntPtr markerPtr)
            {
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
            }
        }

        //
        // 摘要:
        //     Constructs a new performance marker for code instrumentation.
        //
        // 参数:
        //   name:
        //     Marker name.
        //
        //   category:
        //     Profiler category.
        //
        //   nameLen:
        //     Marker name length.
        //
        //   flags:
        //     The marker flags.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ProfilerMarker(string name)
        {
        }

        //
        // 摘要:
        //     Constructs a new performance marker for code instrumentation.
        //
        // 参数:
        //   name:
        //     Marker name.
        //
        //   category:
        //     Profiler category.
        //
        //   nameLen:
        //     Marker name length.
        //
        //   flags:
        //     The marker flags.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ProfilerMarker(char* name, int nameLen)
        {
        }

        //
        // 摘要:
        //     Begin profiling a piece of code marked with a custom name defined by this instance
        //     of ProfilerMarker.
        //
        // 参数:
        //   contextUnityObject:
        //     Object associated with the operation.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        [Conditional("ENABLE_PROFILER")]
        public void Begin()
        {
        }

        //
        // 摘要:
        //     End profiling a piece of code marked with a custom name defined by this instance
        //     of ProfilerMarker.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        [Conditional("ENABLE_PROFILER")]
        public void End()
        {
        }


        //
        // 摘要:
        //     Creates a helper struct for the scoped using blocks.
        //
        // 返回结果:
        //     IDisposable struct which calls Begin and End automatically.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public AutoScope Auto()
        {
            return new AutoScope(m_Ptr);
        }
    }

#if ENABLE_PROFILER
    [StructLayout(LayoutKind.Sequential)]
#else
    [StructLayout(LayoutKind.Sequential, Size = 1)]
#endif
    public readonly struct ProfilerMarker<TP1> where TP1 : unmanaged
    {
        /// <summary>
        /// Constructs the ProfilerMarker that belongs to the generic ProfilerCategory.Scripts category.
        /// </summary>
        /// <remarks>Does nothing in Release Players.</remarks>
        /// <param name="name">Name of a marker.</param>
        /// <param name="param1Name">Name of the first parameter passed to the Begin method.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ProfilerMarker(string name, string param1Name)
        {

        }


        /// <summary>
        /// Begins profiling a piece of code marked with the ProfilerMarker instance.
        /// </summary>
        /// <remarks>Does nothing in Release Players.</remarks>
        /// <param name="p1">Additional context parameter.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_PROFILER")]
        [Pure]
        public unsafe void Begin(TP1 p1)
        {
        }

        /// <summary>
        /// Ends profiling a piece of code marked with the ProfilerMarker instance.
        /// </summary>
        /// <remarks>Does nothing in Release Players.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_PROFILER")]
        [Pure]
        public void End()
        {

        }

        /// <summary>
        /// A helper struct that automatically calls End on Dispose. Used with the *using* statement.
        /// </summary>
        public readonly struct AutoScope : IDisposable
        {


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal AutoScope(ProfilerMarker<TP1> marker, TP1 p1)
            {

            }

            /// <summary>
            /// Calls ProfilerMarker.End.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {

            }
        }

        /// <summary>
        /// Profiles a piece of code enclosed within the *using* statement.
        /// </summary>
        /// <remarks>Returns *null* in Release Players.</remarks>
        /// <param name="p1">Additional context parameter.</param>
        /// <returns>IDisposable struct which calls End on Dispose.</returns>
        /// <example>
        /// <code>
        /// using (profilerMarker.Auto(enemies.Count))
        /// {
        ///     var blastRadius2 = blastRadius * blastRadius;
        ///     for (int i = 0; i &lt; enemies.Count; ++i)
        ///     {
        ///         var r2 = (enemies[i].Pos - blastPos).sqrMagnitude;
        ///         if (r2 &lt; blastRadius2)
        ///             enemies[i].Dispose();
        ///     }
        /// }
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Pure]
        public AutoScope Auto(TP1 p1)
        {
#if ENABLE_PROFILER
            return new AutoScope(this, p1);
#else
            return default;
#endif
        }
    }
}

namespace Unity.Collections
{
    namespace LowLevel.Unsafe
    {
        [AttributeUsage(AttributeTargets.Struct)]
        public sealed class NativeContainerSupportsDeferredConvertListToArray : Attribute
        {
        }
        [AttributeUsage(AttributeTargets.Struct)]
        public sealed class NativeContainerSupportsDeallocateOnJobCompletionAttribute : Attribute
        {
        }
        [AttributeUsage(AttributeTargets.Struct)]
        public sealed class NativeContainerAttribute : Attribute
        {
        }
        [AttributeUsage(AttributeTargets.Struct)]
        public sealed class NativeContainerSupportsMinMaxWriteRestrictionAttribute : Attribute
        {
        }
        [AttributeUsage(AttributeTargets.Struct)]
        public sealed class NativeContainerIsReadOnlyAttribute : Attribute
        {
        }

        public struct AtomicSafetyHandle
        {
            internal GCHandle m_SafetyHandle;
            public static AtomicSafetyHandle Create()
            {
                return new AtomicSafetyHandle()
                {
                    m_SafetyHandle = GCHandle.Alloc(new object(), GCHandleType.Normal)
                };
            }

            internal static void CheckReadAndThrowNoEarlyOut(AtomicSafetyHandle handle)
            {
                //if (!handle.m_SafetyHandle.IsAllocated)
                //    throw new InvalidOperationException("AtomicSafetyHandle is not valid");
            }

            internal static void CheckWriteAndThrowNoEarlyOut(AtomicSafetyHandle handle)
            {
                //if (!handle.m_SafetyHandle.IsAllocated)
                //    throw new InvalidOperationException("AtomicSafetyHandle is not valid");
            }

            public unsafe static void CheckReadAndThrow(AtomicSafetyHandle handle)
            {
                CheckReadAndThrowNoEarlyOut(handle);
            }
            public unsafe static void CheckWriteAndThrow(AtomicSafetyHandle handle)
            {
                CheckWriteAndThrowNoEarlyOut(handle);
            }
        }

        public unsafe static class UnsafeUtility
        {
            [StructLayout(LayoutKind.Sequential, Size = 1)]
            internal struct IsUnmanagedCache<T>
            {
                internal static int value;
            }

            [StructLayout(LayoutKind.Sequential, Size = 1)]
            internal struct IsValidNativeContainerElementTypeCache<T>
            {
                internal static int value;
            }

            private struct AlignOfHelper<T> where T : struct
            {
                public byte dummy;

                public T data;
            }

            public static int AlignOf<T>() where T : unmanaged
            {
                return SizeOf<AlignOfHelper<T>>() - SizeOf<T>();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe static void CopyPtrToStructure<T>(void* ptr, out T output) where T : struct
            {
                if (ptr == null)
                {
                    throw new ArgumentNullException();
                }

                InternalCopyPtrToStructure<T>(ptr, out output);
            }

            private unsafe static void InternalCopyPtrToStructure<T>(void* ptr, out T output) where T : struct
            {
                output = *(T*)ptr;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe static void CopyStructureToPtr<T>(ref T input, void* ptr) where T : unmanaged
            {
                if (ptr == null)
                {
                    throw new ArgumentNullException();
                }

                InternalCopyStructureToPtr(ref input, ptr);
            }

            private unsafe static void InternalCopyStructureToPtr<T>(ref T input, void* ptr) where T : unmanaged
            {
                *(T*)ptr = input;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe static T ReadArrayElement<T>(void* source, int index) where T : unmanaged
            {
                return *(T*)((byte*)source + (long)index * (long)sizeof(T));
            }

            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            //public unsafe static T ReadArrayElementWithStride<T>(void* source, int index, int stride) where T : unmanaged
            //{
            //    return *(T*)((byte*)source + (long)index * (long)stride);
            //}

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe static void WriteArrayElement<T>(void* destination, int index, T value) where T : unmanaged
            {
                *(T*)((byte*)destination + (long)index * (long)sizeof(T)) = value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe static void WriteArrayElementWithStride<T>(void* destination, int index, int stride, T value) where T : unmanaged
            {
                *(T*)((byte*)destination + (long)index * (long)stride) = value;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe static void* AddressOf<T>(ref T output) where T : struct
            {
                return System.Runtime.CompilerServices.Unsafe.AsPointer(ref output);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe static int SizeOf<T>() where T : unmanaged
            {
                return sizeof(T);
            }

            public static ref T As<U, T>(ref U from)
            {
                return ref System.Runtime.CompilerServices.Unsafe.As<U, T>(ref from);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe static ref T AsRef<T>(void* ptr) where T : struct
            {
                return ref *(T*)ptr;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe static ref T ArrayElementAsRef<T>(void* ptr, int index) where T : struct
            {
                return ref *(T*)((byte*)ptr + (long)index * (long)sizeof(T));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int EnumToInt<T>(T enumValue) where T : struct, IConvertible
            {
                int intValue = 0;
                InternalEnumToInt(ref enumValue, ref intValue);
                return intValue;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void InternalEnumToInt<T>(ref T enumValue, ref int intValue)
            {
                intValue = System.Runtime.CompilerServices.Unsafe.As<T, int>(ref enumValue);
            }


            //public unsafe static extern void MemCpyReplicate(void* destination, void* source, int size, int count);


            public unsafe static void MemSet(void* destination, byte value, long size)
            { 
                for (int i = 0; i < size; i++)
                {
                    ((byte*)destination)[i] = value;
                }
            }

            public unsafe static void MemClear(void* destination, long size)
            {
                MemSet(destination, 0, size);
            }

            public unsafe static int MemCmp(void* ptr1, void* ptr2, long size)
            {
                byte* p1 = (byte*)ptr1;
                byte* p2 = (byte*)ptr2;

                for (long i = 0; i < size; i++)
                {
                    if (p1[i] < p2[i]) return -1;
                    if (p1[i] > p2[i]) return 1;
                }

                return 0;
            }

            public unsafe static void* MallocTracked(long size, int alignment, int callstacksToSkip)
            {
                if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
                if (alignment <= 0) throw new ArgumentOutOfRangeException(nameof(alignment));

                // 分配内存
                void* ptr = (void*)Marshal.AllocHGlobal((IntPtr)size);
                if (ptr == null)
                {
                    return null;
                }

                // 确保对齐
                IntPtr alignedPtr = new IntPtr(((long)ptr + alignment - 1) & ~(alignment - 1));
                return (void*)alignedPtr;
            }

            //
            // 摘要:
            //     Free memory with leak tracking.
            //
            // 参数:
            //   memory:
            //     Memory pointer.
            //
            //   allocator:
            //     Allocator.
            public unsafe static void FreeTracked(void* memory)
            {
                if (memory != null)
                {
                    Marshal.FreeHGlobal((IntPtr)memory);
                }
            }

            public unsafe static void* Malloc(long size, int alignment, Allocator label)
            {
                return System.Runtime.InteropServices.NativeMemory.AlignedAlloc((uint)size, (uint)alignment);
            }

            //
            // 摘要:
            //     Free memory.
            //
            // 参数:
            //   memory:
            //     Memory pointer.
            //
            //   allocator:
            //     Allocator.
            public unsafe static void Free(void* memory, Allocator allocator)
            {
                System.Runtime.InteropServices.NativeMemory.AlignedFree(memory);
            }

            public static bool MemCmp(byte* ptr, byte* other, uint length)
            {
                for (int i = 0; i < length; i++)
                {
                    if (ptr[i] != other[i])
                        return false;
                }

                return true;
            }

            public static bool MemCmp(void* ptr, void* other, uint length)
            {
                return MemCmp((byte*)ptr, (byte*)other, length);
            }

            public static void MemCpyStride(void* dst, int dstStride, void* src, int srcStride, int elementSize, int elementCount)
            {
                byte* dstPtr = (byte*)dst;
                byte* srcPtr = (byte*)src;

                for (int i = 0; i < elementCount; i++)
                {
                    Buffer.MemoryCopy(srcPtr, dstPtr, elementSize, elementSize);
                    dstPtr += dstStride;
                    srcPtr += srcStride;
                }
            }

            public unsafe static void MemCpy(void* destination, void* source, long size)
            {
                NativeMemory.Copy(source, destination, (uint)size);
            }

            [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
            public static extern void MemMove(void* dest, void* src, [MarshalAs(UnmanagedType.U4)] int length);


            public unsafe static void* PinGCArrayAndGetDataAddress(Array target, out ulong gcHandle)
            {
                if (target == null)
                {
                    throw new ArgumentNullException("target");
                }
                var gcHandleObject = GCHandle.Alloc(target, GCHandleType.Pinned);
                var handlePtr = GCHandle.ToIntPtr(gcHandleObject);
                gcHandle = (ulong)handlePtr.ToInt64();
                return handlePtr.ToPointer();
            }

            public static void ReleaseGCObject(ulong gcHandle)
            {
               var gcHandleObject =  GCHandle.FromIntPtr((int)gcHandle);
                if (gcHandleObject.IsAllocated)
                {
                    gcHandleObject.Free();
                }
            }
        }

        public static class NativeArrayUnsafeUtility
        {
            public static AtomicSafetyHandle GetAtomicSafetyHandle<T>(NativeArray<T> array) where T : unmanaged
            {
                return array.m_Safety;
            }

            public static void SetAtomicSafetyHandle<T>(ref NativeArray<T> array, AtomicSafetyHandle safety) where T : unmanaged
            {
                array.m_Safety = safety;
            }
            private static void CheckConvertArguments<T>(int length) where T : struct
            {
                if (length < 0)
                {
                    throw new ArgumentOutOfRangeException("length", "Length must be >= 0");
                }
            }

            public unsafe static NativeArray<T> ConvertExistingDataToNativeArray<T>(void* dataPointer, int length) where T : unmanaged
            {
                CheckConvertArguments<T>(length);
                NativeArray<T> result = default(NativeArray<T>);
                result.m_Buffer = dataPointer;
                result.m_Length = length;
                result.m_MinIndex = 0;
                result.m_MaxIndex = length - 1;
                return result;
            }

            public unsafe static void* GetUnsafePtr<T>(this NativeArray<T> nativeArray) where T : unmanaged
            {
                AtomicSafetyHandle.CheckWriteAndThrow(nativeArray.m_Safety);
                return nativeArray.m_Buffer;
            }

            public unsafe static void* GetUnsafeReadOnlyPtr<T>(this NativeArray<T> nativeArray) where T : unmanaged
            {
                AtomicSafetyHandle.CheckReadAndThrow(nativeArray.m_Safety);
                return nativeArray.m_Buffer;
            }

            public unsafe static void* GetUnsafeReadOnlyPtr<T>(this NativeArray<T>.ReadOnly nativeArray) where T : unmanaged
            {
                AtomicSafetyHandle.CheckReadAndThrow(nativeArray.m_Safety);
                return nativeArray.m_Buffer;
            }

            public unsafe static void* GetUnsafeBufferPointerWithoutChecks<T>(NativeArray<T> nativeArray) where T : unmanaged
            {
                return nativeArray.m_Buffer;
            }
        }
    }

    internal sealed class NativeArrayDebugView<T> where T : unmanaged
    {
        private NativeArray<T> m_Array;

        public T[] Items => m_Array.ToArray();

        public NativeArrayDebugView(NativeArray<T> array)
        {
            m_Array = array;
        }
    }
    [NativeContainerSupportsDeferredConvertListToArray]
    [NativeContainerSupportsMinMaxWriteRestriction]
    [NativeContainerSupportsDeallocateOnJobCompletion]
    [NativeContainer]
    [DebuggerTypeProxy(typeof(NativeArrayDebugView<>))]
    [DebuggerDisplay("Length = {Length}")]
    public struct NativeArray<T> : IDisposable, IEnumerable<T>, IEnumerable, IEquatable<NativeArray<T>> where T : unmanaged
    {
        public struct Enumerator : IEnumerator<T>, IEnumerator, IDisposable
        {
            private NativeArray<T> m_Array;

            private int m_Index;

            private T value;

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return value;
                }
            }

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return Current;
                }
            }

            public Enumerator(ref NativeArray<T> array)
            {
                m_Array = array;
                m_Index = -1;
                value = default(T);
            }

            public void Dispose()
            {
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe bool MoveNext()
            {
                m_Index++;
                if (m_Index < m_Array.m_Length)
                {
                    AtomicSafetyHandle.CheckReadAndThrow(m_Array.m_Safety);
                    value = UnsafeUtility.ReadArrayElement<T>(m_Array.m_Buffer, m_Index);
                    return true;
                }

                value = default(T);
                return false;
            }

            public void Reset()
            {
                m_Index = -1;
            }
        }

        //
        // 摘要:
        //     NativeArray interface constrained to read-only operation.
        [NativeContainerIsReadOnly]
        [DebuggerDisplay("Length = {Length}")]
        [DebuggerTypeProxy(typeof(NativeArrayReadOnlyDebugView<>))]
        [NativeContainer]
        public struct ReadOnly : IEnumerable<T>, IEnumerable
        {
            public struct Enumerator : IEnumerator<T>, IEnumerator, IDisposable
            {
                private ReadOnly m_Array;

                private int m_Index;

                private T value;

                public T Current
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    get
                    {
                        return value;
                    }
                }

                object IEnumerator.Current => Current;

                public Enumerator(in ReadOnly array)
                {
                    m_Array = array;
                    m_Index = -1;
                    value = default(T);
                }

                public void Dispose()
                {
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public unsafe bool MoveNext()
                {
                    m_Index++;
                    if (m_Index < m_Array.m_Length)
                    {
                        AtomicSafetyHandle.CheckReadAndThrow(m_Array.m_Safety);
                        value = UnsafeUtility.ReadArrayElement<T>(m_Array.m_Buffer, m_Index);
                        return true;
                    }

                    value = default(T);
                    return false;
                }

                public void Reset()
                {
                    m_Index = -1;
                }
            }

            internal unsafe void* m_Buffer;

            internal int m_Length;

            internal AtomicSafetyHandle m_Safety;

            public int Length
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return m_Length;
                }
            }

            public unsafe T this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    CheckElementReadAccess(index);
                    return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
                }
            }

            public unsafe bool IsCreated
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return m_Buffer != null;
                }
            }

            internal unsafe ReadOnly(void* buffer, int length, ref AtomicSafetyHandle safety)
            {
                m_Buffer = buffer;
                m_Length = length;
                m_Safety = safety;
            }

            public void CopyTo(T[] array)
            {
                NativeArray<T>.Copy(this, array);
            }

            public void CopyTo(NativeArray<T> array)
            {
                NativeArray<T>.Copy(this, array);
            }

            public T[] ToArray()
            {
                T[] array = new T[m_Length];
                NativeArray<T>.Copy(this, array, m_Length);
                return array;
            }

            public unsafe NativeArray<U>.ReadOnly Reinterpret<U>() where U : unmanaged
            {
                NativeArray<T>.CheckReinterpretSize<U>();
                return new NativeArray<U>.ReadOnly(m_Buffer, m_Length, ref m_Safety);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private void CheckElementReadAccess(int index)
            {
                if ((uint)index >= (uint)m_Length)
                {
                    throw new IndexOutOfRangeException($"Index {index} is out of range (must be between 0 and {m_Length - 1}).");
                }

                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            }

            public Enumerator GetEnumerator()
            {
                return new Enumerator(in this);
            }

            IEnumerator<T> IEnumerable<T>.GetEnumerator()
            {
                return GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        internal unsafe void* m_Buffer;

        internal int m_Length;

        internal int m_MinIndex;

        internal int m_MaxIndex;

        internal AtomicSafetyHandle m_Safety;

        private static int s_staticSafetyId;
        internal Allocator m_AllocatorLabel;

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_Length;
            }
        }

        public unsafe T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckElementReadAccess(index);
                return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                CheckElementWriteAccess(index);
                UnsafeUtility.WriteArrayElement(m_Buffer, index, value);
            }
        }

        public unsafe bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_Buffer != null;
            }
        }

        public unsafe NativeArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            Allocate(length, allocator, out this);
            if ((options & NativeArrayOptions.ClearMemory) == NativeArrayOptions.ClearMemory)
            {
                UnsafeUtility.MemClear(m_Buffer, (long)Length * (long)UnsafeUtility.SizeOf<T>());
            }
        }

        public NativeArray(T[] array, Allocator allocator)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            Allocate(array.Length, allocator, out this);
            Copy(array, this);
        }

        public NativeArray(NativeArray<T> array, Allocator allocator)
        {
            AtomicSafetyHandle.CheckReadAndThrow(array.m_Safety);
            Allocate(array.Length, allocator, out this);
            Copy(array, 0, this, 0, array.Length);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckAllocateArguments(int length, long totalSize)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", "Length must be >= 0");
            }
        }

        private unsafe static void Allocate(int length, Allocator allocator, out NativeArray<T> array)
        {
            long num = (long)UnsafeUtility.SizeOf<T>() * (long)length;
            CheckAllocateArguments(length, num);
            array = default(NativeArray<T>);
            array.m_Buffer = UnsafeUtility.Malloc(num, UnsafeUtility.AlignOf<T>(), allocator);
            array.m_Length = length;
            array.m_AllocatorLabel = allocator;
            array.m_MinIndex = 0;
            array.m_MaxIndex = length - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckElementReadAccess(int index)
        {
            if (index < m_MinIndex || index > m_MaxIndex)
            {
                FailOutOfRangeError(index);
            }

            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckElementWriteAccess(int index)
        {
            if (index < m_MinIndex || index > m_MaxIndex)
            {
                FailOutOfRangeError(index);
            }

            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
        }

        public unsafe void Dispose()
        {
            if (m_Buffer == null)
            {
                throw new ObjectDisposedException("The NativeArray is already disposed.");
            }

            UnsafeUtility.Free(m_Buffer, m_AllocatorLabel); 
            m_Buffer = null;
        }


        public void CopyFrom(T[] array)
        {
            Copy(array, this);
        }

        public void CopyFrom(NativeArray<T> array)
        {
            Copy(array, this);
        }

        public void CopyTo(T[] array)
        {
            Copy(this, array);
        }

        public void CopyTo(NativeArray<T> array)
        {
            Copy(this, array);
        }

        public T[] ToArray()
        {
            T[] array = new T[Length];
            Copy(this, array, Length);
            return array;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void FailOutOfRangeError(int index)
        {
            if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
            {
                throw new IndexOutOfRangeException($"Index {index} is out of restricted IJobParallelFor range [{m_MinIndex}...{m_MaxIndex}] in ReadWriteBuffer.\n" + "ReadWriteBuffers are restricted to only read & write the element at the job index. You can use double buffering strategies to avoid race conditions due to reading & writing in parallel to the same elements from a job.");
            }

            throw new IndexOutOfRangeException($"Index {index} is out of range of '{Length}' Length.");
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public unsafe bool Equals(NativeArray<T> other)
        {
            return m_Buffer == other.m_Buffer && m_Length == other.m_Length;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return obj is NativeArray<T> && Equals((NativeArray<T>)obj);
        }

        public unsafe override int GetHashCode()
        {
            return ((int)m_Buffer * 397) ^ m_Length;
        }

        public static bool operator ==(NativeArray<T> left, NativeArray<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NativeArray<T> left, NativeArray<T> right)
        {
            return !left.Equals(right);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckCopyLengths(int srcLength, int dstLength)
        {
            if (srcLength != dstLength)
            {
                throw new ArgumentException("source and destination length must be the same");
            }
        }

        public static void Copy(NativeArray<T> src, NativeArray<T> dst)
        {
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            CheckCopyLengths(src.Length, dst.Length);
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(ReadOnly src, NativeArray<T> dst)
        {
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            CheckCopyLengths(src.Length, dst.Length);
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(T[] src, NativeArray<T> dst)
        {
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            CheckCopyLengths(src.Length, dst.Length);
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(NativeArray<T> src, T[] dst)
        {
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            CheckCopyLengths(src.Length, dst.Length);
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(ReadOnly src, T[] dst)
        {
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            CheckCopyLengths(src.Length, dst.Length);
            Copy(src, 0, dst, 0, src.Length);
        }

        public static void Copy(NativeArray<T> src, NativeArray<T> dst, int length)
        {
            Copy(src, 0, dst, 0, length);
        }

        public static void Copy(ReadOnly src, NativeArray<T> dst, int length)
        {
            Copy(src, 0, dst, 0, length);
        }

        public static void Copy(T[] src, NativeArray<T> dst, int length)
        {
            Copy(src, 0, dst, 0, length);
        }

        public static void Copy(NativeArray<T> src, T[] dst, int length)
        {
            Copy(src, 0, dst, 0, length);
        }

        public static void Copy(ReadOnly src, T[] dst, int length)
        {
            Copy(src, 0, dst, 0, length);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckCopyArguments(int srcLength, int srcIndex, int dstLength, int dstIndex, int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", "length must be equal or greater than zero.");
            }

            if (srcIndex < 0 || srcIndex > srcLength || (srcIndex == srcLength && srcLength > 0))
            {
                throw new ArgumentOutOfRangeException("srcIndex", "srcIndex is outside the range of valid indexes for the source NativeArray.");
            }

            if (dstIndex < 0 || dstIndex > dstLength || (dstIndex == dstLength && dstLength > 0))
            {
                throw new ArgumentOutOfRangeException("dstIndex", "dstIndex is outside the range of valid indexes for the destination NativeArray.");
            }

            if (srcIndex + length > srcLength)
            {
                throw new ArgumentException("length is greater than the number of elements from srcIndex to the end of the source NativeArray.", "length");
            }

            if (srcIndex + length < 0)
            {
                throw new ArgumentException("srcIndex + length causes an integer overflow");
            }

            if (dstIndex + length > dstLength)
            {
                throw new ArgumentException("length is greater than the number of elements from dstIndex to the end of the destination NativeArray.", "length");
            }

            if (dstIndex + length < 0)
            {
                throw new ArgumentException("dstIndex + length causes an integer overflow");
            }
        }

        public unsafe static void Copy(NativeArray<T> src, int srcIndex, NativeArray<T> dst, int dstIndex, int length)
        {
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
            UnsafeUtility.MemCpy((byte*)dst.m_Buffer + dstIndex * UnsafeUtility.SizeOf<T>(), (byte*)src.m_Buffer + srcIndex * UnsafeUtility.SizeOf<T>(), length * UnsafeUtility.SizeOf<T>());
        }

        public unsafe static void Copy(ReadOnly src, int srcIndex, NativeArray<T> dst, int dstIndex, int length)
        {
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
            UnsafeUtility.MemCpy((byte*)dst.m_Buffer + dstIndex * UnsafeUtility.SizeOf<T>(), (byte*)src.m_Buffer + srcIndex * UnsafeUtility.SizeOf<T>(), length * UnsafeUtility.SizeOf<T>());
        }

        public unsafe static void Copy(T[] src, int srcIndex, NativeArray<T> dst, int dstIndex, int length)
        {
            AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety);
            if (src == null)
            {
                throw new ArgumentNullException("src");
            }

            CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
            GCHandle gCHandle = GCHandle.Alloc(src, GCHandleType.Pinned);
            IntPtr intPtr = gCHandle.AddrOfPinnedObject();
            UnsafeUtility.MemCpy((byte*)dst.m_Buffer + dstIndex * UnsafeUtility.SizeOf<T>(), (byte*)(void*)intPtr + srcIndex * UnsafeUtility.SizeOf<T>(), length * UnsafeUtility.SizeOf<T>());
            gCHandle.Free();
        }

        public unsafe static void Copy(NativeArray<T> src, int srcIndex, T[] dst, int dstIndex, int length)
        {
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            if (dst == null)
            {
                throw new ArgumentNullException("dst");
            }

            CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
            GCHandle gCHandle = GCHandle.Alloc(dst, GCHandleType.Pinned);
            IntPtr intPtr = gCHandle.AddrOfPinnedObject();
            UnsafeUtility.MemCpy((byte*)(void*)intPtr + dstIndex * UnsafeUtility.SizeOf<T>(), (byte*)src.m_Buffer + srcIndex * UnsafeUtility.SizeOf<T>(), length * UnsafeUtility.SizeOf<T>());
            gCHandle.Free();
        }

        public unsafe static void Copy(ReadOnly src, int srcIndex, T[] dst, int dstIndex, int length)
        {
            AtomicSafetyHandle.CheckReadAndThrow(src.m_Safety);
            if (dst == null)
            {
                throw new ArgumentNullException("dst");
            }

            CheckCopyArguments(src.Length, srcIndex, dst.Length, dstIndex, length);
            GCHandle gCHandle = GCHandle.Alloc(dst, GCHandleType.Pinned);
            IntPtr intPtr = gCHandle.AddrOfPinnedObject();
            UnsafeUtility.MemCpy((byte*)(void*)intPtr + dstIndex * UnsafeUtility.SizeOf<T>(), (byte*)src.m_Buffer + srcIndex * UnsafeUtility.SizeOf<T>(), length * UnsafeUtility.SizeOf<T>());
            gCHandle.Free();
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckReinterpretLoadRange<U>(int sourceIndex) where U : unmanaged
        {
            long num = UnsafeUtility.SizeOf<T>();
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
            long num2 = UnsafeUtility.SizeOf<U>();
            long num3 = Length * num;
            long num4 = sourceIndex * num;
            long num5 = num4 + num2;
            if (num4 < 0 || num5 > num3)
            {
                throw new ArgumentOutOfRangeException("sourceIndex", "loaded byte range must fall inside container bounds");
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckReinterpretStoreRange<U>(int destIndex) where U : unmanaged
        {
            long num = UnsafeUtility.SizeOf<T>();
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
            long num2 = UnsafeUtility.SizeOf<U>();
            long num3 = Length * num;
            long num4 = destIndex * num;
            long num5 = num4 + num2;
            if (num4 < 0 || num5 > num3)
            {
                throw new ArgumentOutOfRangeException("destIndex", "stored byte range must fall inside container bounds");
            }
        }

        public unsafe U ReinterpretLoad<U>(int sourceIndex) where U : unmanaged
        {
            CheckReinterpretLoadRange<U>(sourceIndex);
            byte* source = (byte*)m_Buffer + (long)UnsafeUtility.SizeOf<T>() * (long)sourceIndex;
            return UnsafeUtility.ReadArrayElement<U>(source, 0);
        }

        public unsafe void ReinterpretStore<U>(int destIndex, U data)where U : unmanaged
        {
            CheckReinterpretStoreRange<U>(destIndex);
            byte* destination = (byte*)m_Buffer + (long)UnsafeUtility.SizeOf<T>() * (long)destIndex;
            UnsafeUtility.WriteArrayElement(destination, 0, data);
        }

        private unsafe NativeArray<U> InternalReinterpret<U>(int length) where U : unmanaged
        {
            NativeArray<U> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<U>(m_Buffer, length);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_Safety);
            return array;
        }


        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void CheckReinterpretSize<U>() where U : unmanaged
        {
            if (UnsafeUtility.SizeOf<T>() != UnsafeUtility.SizeOf<U>())
            {
                throw new InvalidOperationException($"Types {typeof(T)} and {typeof(U)} are different sizes - direct reinterpretation is not possible. If this is what you intended, use Reinterpret(<type size>)");
            }
        }

        public NativeArray<U> Reinterpret<U>() where U : unmanaged
        {
            CheckReinterpretSize<U>();
            return InternalReinterpret<U>(Length);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckReinterpretSize<U>(long tSize, long uSize, int expectedTypeSize, long byteLen, long uLen)
        {
            if (tSize != expectedTypeSize)
            {
                throw new InvalidOperationException($"Type {typeof(T)} was expected to be {expectedTypeSize} but is {tSize} bytes");
            }

            if (uLen * uSize != byteLen)
            {
                throw new InvalidOperationException($"Types {typeof(T)} (array length {Length}) and {typeof(U)} cannot be aliased due to size constraints. The size of the types and lengths involved must line up.");
            }
        }

        public NativeArray<U> Reinterpret<U>(int expectedTypeSize) where U : unmanaged
        {
            long num = UnsafeUtility.SizeOf<T>();
            long num2 = UnsafeUtility.SizeOf<U>();
            long num3 = Length * num;
            long num4 = num3 / num2;
            CheckReinterpretSize<U>(num, num2, expectedTypeSize, num3, num4);
            return InternalReinterpret<U>((int)num4);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private void CheckGetSubArrayArguments(int start, int length)
        {
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException("start", "start must be >= 0");
            }

            if (start + length > Length)
            {
                throw new ArgumentOutOfRangeException("length", $"sub array range {start}-{start + length - 1} is outside the range of the native array 0-{Length - 1}");
            }

            if (start + length < 0)
            {
                throw new ArgumentException($"sub array range {start}-{start + length - 1} caused an integer overflow and is outside the range of the native array 0-{Length - 1}");
            }
        }

        public unsafe NativeArray<T> GetSubArray(int start, int length)
        {
            CheckGetSubArrayArguments(start, length);
            NativeArray<T> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>((byte*)m_Buffer + (long)UnsafeUtility.SizeOf<T>() * (long)start, length);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_Safety);
            return array;
        }

        public unsafe ReadOnly AsReadOnly()
        {
            return new ReadOnly(m_Buffer, m_Length, ref m_Safety);
        }
    }

    internal sealed class NativeArrayReadOnlyDebugView<T> where T : unmanaged
    {
        private NativeArray<T>.ReadOnly m_Array;

        public T[] Items => m_Array.ToArray();

        public NativeArrayReadOnlyDebugView(NativeArray<T>.ReadOnly array)
        {
            m_Array = array;
        }
    }

    public enum Allocator
    {
        //
        // 摘要:
        //     Invalid allocation.
        Invalid,
        //
        // 摘要:
        //     No allocation.
        None,
        //
        // 摘要:
        //     Temporary allocation.
        Temp,
        //
        // 摘要:
        //     Temporary job allocation.
        TempJob,
        //
        // 摘要:
        //     Persistent allocation.
        Persistent,
        //
        // 摘要:
        //     Allocation associated with a DSPGraph audio kernel.
        AudioKernel
    }

    public enum NativeArrayOptions
    {
        //
        // 摘要:
        //     Uninitialized memory can improve performance, but results in the contents of
        //     the array elements being undefined. In performance sensitive code it can make
        //     sense to use NativeArrayOptions.Uninitialized, if you are writing to the entire
        //     array right after creating it without reading any of the elements first.
        UninitializedMemory,
        //
        // 摘要:
        //     Clear NativeArray memory on allocation.
        ClearMemory
    }
}

namespace Unity.IO.LowLevel.Unsafe
{
    //
    // 摘要:
    //     Describes the offset, size, and destination buffer of a single read operation.
    public struct ReadCommand
    {
        //
        // 摘要:
        //     The buffer that receives the read data.
        public unsafe void* Buffer;

        //
        // 摘要:
        //     The offset where the read begins, within the file.
        public long Offset;

        //
        // 摘要:
        //     The size of the read in bytes.
        public long Size;
    }

    public enum ReadStatus
    {
        //
        // 摘要:
        //     The asynchronous file request completed successfully and all read operations
        //     within it were completed in full.
        Complete = 0,
        //
        // 摘要:
        //     The asynchronous file request is in progress.
        InProgress = 1,
        //
        // 摘要:
        //     One or more of the asynchronous file request's read operations have failed.
        Failed = 2,
        //
        // 摘要:
        //     The asynchronous file request has completed but one or more of the read operations
        //     were truncated.
        Truncated = 4,
        //
        // 摘要:
        //     The asynchronous file request was canceled before the read operations were completed.
        Canceled = 5
    }
    public struct ReadHandle : IDisposable
    {
        internal ReadStatus status;
        internal int readCount;

        //
        // 摘要:
        //     Current state of the read operation.
        public ReadStatus Status=> status;
        

        //
        // 摘要:
        //     The number of read commands performed for this read operation. Will return zero
        //     until the reads have begun.
        public long ReadCount=> readCount;

        //
        // 摘要:
        //     Check if the ReadHandle is valid.
        //
        // 返回结果:
        //     True if the ReadHandle is valid.
        public bool IsValid()
        {
            return true;
        }

        //
        // 摘要:
        //     Disposes the ReadHandle. Use this to free up internal resources for reuse.
        public void Dispose()
        {
        }        
    }
}
namespace UnityEditor
{
    public class EditorUtility
    {
        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));

            string[] sizes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        public static void ClearProgressBar() { 
        
        }

        public static void DisplayProgressBar(string title, string info, float progress)
        {
            Debug.WriteLine($"{title} {info} {progress * 100:0.00}%");
        }
    }
}
