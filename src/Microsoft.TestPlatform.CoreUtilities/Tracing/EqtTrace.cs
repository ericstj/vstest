// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Text;

#if NET46
    using System.Configuration;
    using Microsoft.Win32;
    using Utilities;
    using System.IO;
#endif

    /// <summary>
    /// Wrapper class for tracing.
    ///     - Shortcut-methods for Error, Warning, Info, Verbose. 
    ///     - Adds additional information to the trace: calling process name, PID, ThreadID, Time.
    ///     - Uses custom switch EqtTraceLevel from .config file. 
    ///     - By default tracing if OFF.
    ///     - Our build environment always sets the /d:TRACE so this class is always enabled,
    ///       the Debug class is enabled only in debug builds (/d:DEBUG).
    ///     - We ignore exceptions thrown by underlying TraceSwitch (e.g. due to config file error).
    ///       We log ignored exceptions to system Application log.
    ///       We pass through exceptions thrown due to incorrect arguments to EqtTrace methods.
    /// Usage: EqtTrace.Info("Here's how to trace info");
    /// </summary>
    public static class EqtTrace
    {

#if NET46
        /// <summary>
        /// To which system event log we log if cannot log to trace file.
        /// </summary>
        private const string SystemEventLogSource = "Application";

        /// <summary>
        /// Name of the trace listener.
        /// </summary>
        private const string ListenerName = "TptTraceListener";

        /// <summary>
        /// Maximum size of the trace log file (in kilobytes).
        /// </summary>
        private const string TraceLogMaxFileSizeInKB = "TraceLogMaxFileSizeInKb";

        /// <summary>
        /// The switch for tracing TestPlaform messages only. Initialize Trace listener as a part of this
        /// to avoid a separate Static concstructor (to fix CA1810)
        /// </summary>
        private static readonly TraceSwitch traceLevelSwitch = new TraceSwitch("TpTraceLevel", null);
        
        // Current process name/id that called trace so that it's easier to read logs.
        // We cache them for performance reason.
        private static readonly string processName = GetProcessName();

        private static readonly int processId = GetProcessId();

        /// <summary>
        /// We log to system log only this # of times in order not to overflow it.
        /// </summary>
        //private static int timesCanWriteToSystemLog = 10;
        
        /// Specifies whether the trace is initialized or not
        /// </summary>
        private static bool isInitialized = false;

        /// <summary>
        /// Trace listener object to which all Testplatform traces are written.
        /// </summary>
        private static TraceListener listener;

        /// <summary>
        /// Lock over initialization
        /// </summary>
        private static object initializationLock = new object();

        private static int TraceFileSize = 0;
        private static int DefaultTraceFileSize = 10240; // 10Mb.

        /// <summary>
        /// Log file name to setup custom file for logging.
        /// </summary>
        private static string logFileName = null;
#endif
        /// <summary>
        /// Ensure the trace is initialized
        /// </summary>
        static void EnsureTraceIsInitialized()
        {
#if NET46
            if (isInitialized)
            {
                return;
            }

            lock (initializationLock)
            {
                if (isInitialized)
                {
                    return;
                }

                string logsDirectory = Path.GetTempPath();
                // Set the trace level and add the trace listener

                if (logFileName == null)
                {
                    logFileName =
                    Path.Combine(
                            logsDirectory,
                            Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName) +
                                ".TpTrace.log"
                        );
                }


                string traceFileSizeStr = ConfigurationManager.AppSettings[TraceLogMaxFileSizeInKB];
                if (!int.TryParse(traceFileSizeStr, out TraceFileSize)
                        || TraceFileSize <= 0)
                {
                    TraceFileSize = DefaultTraceFileSize;
                }

                //listener = new RollingFileTraceListener(logFileName, ListenerName, TraceFileSize);
                Trace.Listeners.Add(listener);
                // Set the auto-flush flag, so that log entries are written immediately. This is done so that if the
                // process exits or is killed, we will have as much log information as possible in the log.
                Trace.AutoFlush = true;

                isInitialized = true;
            }
#endif
        }

#if NET46
        /// <summary>
        /// Setup remote trace listener in the child domain.
        /// If calling domain, doesn't have tracing enabled nothing is done.
        /// </summary>
        /// <param name="childDomain"></param>
        public static void SetupRemoteEqtTraceListeners(AppDomain childDomain)
        {
            if (null != childDomain)
            {
                RemoteEqtTrace remoteEqtTrace = (RemoteEqtTrace)childDomain.CreateInstanceFromAndUnwrap(
                    typeof(RemoteEqtTrace).Assembly.Location,
                    typeof(RemoteEqtTrace).FullName);

                remoteEqtTrace.TraceLevel = TraceLevel;

                if (!Enum.Equals(TraceLevel, TraceLevel.Off))
                {

                    TraceListener tptListner = null;
                    foreach (TraceListener listener in Trace.Listeners)
                    {
                        if (String.Equals(listener.Name, ListenerName, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Assert(tptListner == null, "Multiple TptListeners found.");
                            tptListner = listener;
                        }
                    }
                    remoteEqtTrace.SetupRemoteListeners(tptListner);
                }
            }
        }

        /// <summary>
        /// Setup trace listenrs. It should be called when setting trace listener for child domain.
        /// </summary>
        /// <param name="listener"></param>
        internal static void SetupRemoteListeners(TraceListener traceListener)
        {
            lock (initializationLock)
            {
                // Add new listeners.
                if (null != listener)
                {
                    Trace.Listeners.Add(listener);
                    listener = traceListener;
                }
                isInitialized = true;
            }
        }
#endif

        /// <summary>
        /// Setup trace listenrs. It should be called when setting trace listener for child domain.
        /// </summary>
        /// <param name="listener"></param>
        internal static void SetupRemoteListeners()
        {
        }

        #region TraceLevel, ShouldTrace

#if NET46
        public static System.Diagnostics.TraceLevel TraceLevel
        {
            get
            {
                return traceLevelSwitch.Level;
            }

            set
            {
                try
                {
                    traceLevelSwitch.Level = value;
                }
                catch (ArgumentException e)
                {
                    LogIgnoredException(e);
                }
            }
        }
#endif
        /// <summary>
        ///     Boolean flag to know if tracing error statements is enabled.
        /// </summary>
        public static bool IsErrorEnabled
        {
            get { return false; }
        }

        /// <summary>
        ///     Boolean flag to know if tracing info statements is enabled. 
        /// </summary>
        public static bool IsInfoEnabled
        {
            get { return false; }
        }

        /// <summary>
        ///     Boolean flag to know if tracing verbose statements is enabled. 
        /// </summary>
        public static bool IsVerboseEnabled
        {
            get { return false; }
        }

        /// <summary>
        ///     Boolean flag to know if tracing warning statements is enabled.
        /// </summary>
        public static bool IsWarningEnabled
        {
            get { return false; }
        }

        /// <summary>
        /// returns true if tracing is enabled for the passed
        /// trace level
        /// </summary>
        /// <param name="traceLevel"></param>
        /// <returns></returns>
        public static bool ShouldTrace()
        {
            return false;
        }
        #endregion

        #region Error

                /// <summary>
                /// Prints an error message and prompts with a Debug dialog
                /// </summary>
                /// <param name="message">the error message</param>
                [ConditionalAttribute("TRACE")]
                public static void Fail(string message)
                {
                    Error(message);
                }


                /// <summary>
                /// Combines together EqtTrace.Fail and Debug.Fail:
                /// Prints an formatted error message and prompts with a Debug dialog.
                /// </summary>
                /// <param name="format">the formatted error message</param>
                /// <param name="args">arguments to the format</param>
                [ConditionalAttribute("TRACE")]
                public static void Fail(string format, params object[] args)
                {
                    if (IsErrorEnabled)
                    {
                        var message = string.Format(CultureInfo.InvariantCulture, format, args);
                        Error(message);
        #if DEBUG
                        Debug.Fail(message);
        #endif
                    }
                }



                [ConditionalAttribute("TRACE")]
                public static void Error(string message)
                {
                    // Todo Add logging.
                }

                /// <summary>
                /// Only prints the message if the condition is true
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void ErrorIf(bool condition, string message)
                {
                    if (condition)
                    {
                        Error(message);
                    }
                }

                /// <summary>
                /// Only prints the formatted message if the condition is false
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void ErrorUnless(bool condition, string message)
                {
                    ErrorIf(!condition, message);
                }

                /// <summary>
                /// Prints the message if the condition is false. If the condition is true,
                /// the message is instead printed at the specified trace level.
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void ErrorUnlessAlterTrace(bool condition, string message)
                {
                }

                [ConditionalAttribute("TRACE")]
                public static void Error(string format, params object[] args)
                {
                    if (IsErrorEnabled)
                    {
                        Debug.Assert(format != null);
                    }
                }

                /// <summary>
                /// Only prints the formatted message if the condition is false
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void ErrorUnless(bool condition, string format, params object[] args)
                {
                    ErrorIf(!condition, format, args);
                }

                /// <summary>
                /// Prints the message if the condition is false. If the condition is true,
                /// the message is instead printed at the specified trace level.
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void ErrorUnlessAlterTrace(bool condition, string format, params object[] args)
                {
                }

                /// <summary>
                /// Only prints the formatted message if the condition is true
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void ErrorIf(bool condition, string format, params object[] args)
                {
                    if (condition)
                    {
                        Error(format, args);
                    }
                }

                /// <summary>
                /// EqtTrace.Error and Debug.Fail combined in one call.
                /// </summary>
                /// <param name="message">The message to send to Debug.Fail and EqtTrace.Error.</param>
                /// <param name="args">Params to string.Format.</param>
                [ConditionalAttribute("TRACE")]
                public static void ErrorAssert(string format, params object[] args)
                {
                }

                /// <summary>
                /// Write a exception if tracing for error is enabled
                /// </summary>
                /// <param name="exceptionToTrace">The exception to write.</param>
                [ConditionalAttribute("TRACE")]
                public static void Error(Exception exceptionToTrace)
                {
                    if (IsErrorEnabled)
                    {
                        Debug.Assert(exceptionToTrace != null);
                    }
                }

        #endregion

        #region Warning

                [ConditionalAttribute("TRACE")]
                public static void Warning(string message)
                {
                    if (IsWarningEnabled)
                    {
                        // Add logging.
                    }
                }

                /// <summary>
                /// Only prints the formatted message if the condition is true
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void WarningIf(bool condition, string message)
                {
                    if (condition)
                    {
                        Warning(message);
                    }
                }

                /// <summary>
                /// Only prints the formatted message if the condition is false
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void WarningUnless(bool condition, string message)
                {
                    WarningIf(!condition, message);
                }

                /// <summary>
                /// Prints the message if the condition is false. If the condition is true,
                /// the message is instead printed at the specified trace level.
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void WarningUnlessAlterTrace(bool condition, string message)
                {
                }

                [ConditionalAttribute("TRACE")]
                public static void Warning(string format, params object[] args)
                {
                    if (IsWarningEnabled)
                    {
                        Debug.Assert(format != null);
                    }
                }

                [ConditionalAttribute("TRACE")]
                public static void WarningIf(bool condition, string format, params object[] args)
                {
                    if (condition)
                    {
                        Warning(format, args);
                    }
                }

                /// <summary>
                /// Only prints the formatted message if the condition is false
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void WarningUnless(bool condition, string format, params object[] args)
                {
                    WarningIf(!condition, format, args);
                }

                /// <summary>
                /// Prints the message if the condition is false. If the condition is true,
                /// the message is instead printed at the specified trace level.
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void WarningUnlessAlterTrace(bool condition, string format, params object[] args)
                {
                }
        #endregion

        #region Info

                [ConditionalAttribute("TRACE")]
                public static void Info(string message)
                {
                    if (IsInfoEnabled)
                    {
                        // Add logging.
                    }
                }

                [ConditionalAttribute("TRACE")]
                public static void InfoIf(bool condition, string message)
                {
                    if (condition)
                    {
                        Info(message);
                    }
                }

                /// <summary>
                /// Only prints the formatted message if the condition is false
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void InfoUnless(bool condition, string message)
                {
                    InfoIf(!condition, message);
                }

                /// <summary>
                /// Prints the message if the condition is false. If the condition is true,
                /// the message is instead printed at the specified trace level.
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void InfoUnlessAlterTrace(bool condition, string message)
                {
                }

                [ConditionalAttribute("TRACE")]
                public static void Info(string format, params object[] args)
                {
                    if (IsInfoEnabled)
                    {
                        Debug.Assert(format != null);

                        // Check level before doing string.Format to avoid string creation if tracing is off.
                    }
                }

                [ConditionalAttribute("TRACE")]
                public static void InfoIf(bool condition, string format, params object[] args)
                {
                    if (condition)
                    {
                        Info(format, args);
                    }
                }

                /// <summary>
                /// Only prints the formatted message if the condition is false
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void InfoUnless(bool condition, string format, params object[] args)
                {
                    InfoIf(!condition, format, args);
                }

                /// <summary>
                /// Prints the message if the condition is false. If the condition is true,
                /// the message is instead printed at the specified trace level.
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void InfoUnlessAlterTrace(bool condition, string format, params object[] args)
                {
                }
        #endregion

        #region Verbose

                [ConditionalAttribute("TRACE")]
                public static void Verbose(string message)
                {
                    if (IsVerboseEnabled)
                    {
                        // Add logging.
                    }
                }

                [ConditionalAttribute("TRACE")]
                public static void VerboseIf(bool condition, string message)
                {
                    if (condition)
                    {
                        Verbose(message);
                    }
                }

                /// <summary>
                /// Only prints the formatted message if the condition is false
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void VerboseUnless(bool condition, string message)
                {
                    VerboseIf(!condition, message);
                }

                /// <summary>
                /// Prints the message if the condition is false. If the condition is true,
                /// the message is instead printed at the specified trace level.
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void VerboseUnlessAlterTrace(bool condition, string message)
                {
                }

                [ConditionalAttribute("TRACE")]
                public static void Verbose(string format, params object[] args)
                {
                    if (IsVerboseEnabled)
                    {
                        Debug.Assert(format != null);
                    }
                }

                [ConditionalAttribute("TRACE")]
                public static void VerboseIf(bool condition, string format, params object[] args)
                {
                    if (condition)
                    {
                        Verbose(format, args);
                    }
                }

                /// <summary>
                /// Only prints the formatted message if the condition is false
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void VerboseUnless(bool condition, string format, params object[] args)
                {
                    VerboseIf(!condition, format, args);
                }

                /// <summary>
                /// Prints the message if the condition is false. If the condition is true,
                /// the message is instead printed at the specified trace level.
                /// </summary>
                /// <param name="condition"></param>
                /// <param name="message"></param>
                [ConditionalAttribute("TRACE")]
                public static void VerboseUnlessAlterTrace(bool condition, string format, params object[] args)
                {
                }
        #endregion

        #region Helpers

                /// <summary>
                /// Formats an exception into a nice looking message.
                /// </summary>
                /// <param name="exceptionToTrace">The exception to write.</param>
                /// <returns>The formatted string.</returns>
                private static string FormatException(Exception exceptionToTrace)
                {
                    // Prefix for each line
                    string prefix = Environment.NewLine + '\t';

                    // Format this exception
                    StringBuilder message = new StringBuilder();
                    message.Append(string.Format(CultureInfo.InvariantCulture,
                        "Exception: {0}{1}Message: {2}{3}Stack Trace: {4}",
                        exceptionToTrace.GetType(), prefix, exceptionToTrace.Message,
                        prefix, exceptionToTrace.StackTrace));

                    // If there is base exception, add that to message
                    if (exceptionToTrace.GetBaseException() != null)
                    {
                        message.Append(string.Format(CultureInfo.InvariantCulture,
                            "{0}BaseExceptionMessage: {1}",
                            prefix, exceptionToTrace.GetBaseException().Message));
                    }

                    // If there is inner exception, add that to message
                    // We deliberately avoid recursive calls here.
                    if (exceptionToTrace.InnerException != null)
                    {
                        // Format same as outer exception except
                        // "InnerException" is prefixed to each line
                        Exception inner = exceptionToTrace.InnerException;
                        prefix += "InnerException";
                        message.Append(string.Format(CultureInfo.InvariantCulture,
                            "{0}: {1}{2} Message: {3}{4} Stack Trace: {5}",
                            prefix, inner.GetType(), prefix, inner.Message, prefix, inner.StackTrace));

                        if (inner.GetBaseException() != null)
                        {
                            message.Append(string.Format(CultureInfo.InvariantCulture,
                                "{0}BaseExceptionMessage: {1}",
                                prefix, inner.GetBaseException().Message));
                        }
                    }

                    // Append a new line
                    message.Append(Environment.NewLine);

                    return message.ToString();
                }

                /// <summary>
                /// Get the process name. Note: we cache it, use m_processName.
                /// </summary>
                [SuppressMessage("Microsoft.Design", "CA1031")]
                private static string GetProcessName()
                {
                    return string.Empty;
                }

                private static int GetProcessId()
                {
                    return -1;
                }

                private static void WriteAtLevel(string message)
                {
                }

                private static void WriteAtLevel(string format, params object[] args)
                {
                    Debug.Assert(format != null);
                }

                /// <summary>
                /// Adds the message to the trace log.
                /// The line becomes: 
                ///     [I, PID, ThreadID, 2003/06/11 11:56:07.445] CallingAssemblyName: message.
                /// </summary>
                /// <param name="level"></param>
                /// <param name="message">The message to add to trace.</param>
                [SuppressMessage("Microsoft.Design", "CA1031")]
                [SuppressMessage("Microsoft.Globalization", "CA1303")]
                private static void WriteLine(string message)
                {
                }

                /// <summary>
                /// Auxillary method: logs the exception that is being ignored.
                /// </summary>
                /// <param name="e">The exception to log.</param>
                [SuppressMessage("Microsoft.Design", "CA1031")]
                private static void LogIgnoredException(Exception e)
                {
                }

                private static void WriteEventLogEntry(string message)
                {
                }

        #endregion
    }
}