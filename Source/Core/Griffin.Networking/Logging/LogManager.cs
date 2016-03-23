using System;

namespace Griffin.Networking.Logging
{
    /// <summary>
    /// Logging facade
    /// </summary>
    /// <remarks>Implement an adapter for your own logging framework and assign it using <see cref="Assign"/></remarks>
    public class LogManager
    {
        private static LogManager current;
        private static readonly object SynLock = new object();

        /// <summary>
        /// Get the current adapter
        /// </summary>
        public static LogManager Current
        {
            get
            {
                if (current == null)
                {
                    lock (SynLock)
                    {
                        if (current == null)
                        {
                            //not a problem on x86 & x64
                            // ReSharper disable PossibleMultipleWriteAccessInDoubleCheckLocking
                            current = new LogManager();
                            // ReSharper restore PossibleMultipleWriteAccessInDoubleCheckLocking
                        }
                    }
                }

                return current;
            }
        }

        /// <summary>
        /// Get a logger
        /// </summary>
        /// <typeparam name="T">Type requesting a logger</typeparam>
        /// <returns>A logger</returns>
        public static ILogger GetLogger<T>() where T : class
        {
            return Current.GetLoggerInternal(typeof (T));
        }

        /// <summary>
        /// Get a logger
        /// </summary>
        /// <param name="loggingType">Type that will log messages</param>
        /// <returns>A logger</returns>
        public static ILogger GetLogger(Type loggingType)
        {
            return Current.GetLoggerInternal(loggingType);
        }

        /// <summary>
        /// Get a logger for a type
        /// </summary>
        /// <param name="loggingType">Type that want's a logger</param>
        /// <returns>Default implementation returns a <see cref="NullLogger"/></returns>
        protected virtual ILogger GetLoggerInternal(Type loggingType)
        {
            return new NullLogger();
        }

        /// <summary>
        /// Assign a logging adapter.
        /// </summary>
        /// <param name="logManager">Manager to use</param>
        public static void Assign(LogManager logManager)
        {
            current = logManager;
        }
    }
}