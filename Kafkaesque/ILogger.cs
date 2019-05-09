using System;

namespace Kafkaesque
{
    /// <summary>
    /// Implement to catch logs from Kafkaesque
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs at the most verbose level
        /// </summary>
        void Verbose(string message);

        /// <summary>
        /// Logs at the most verbose level
        /// </summary>
        void Verbose(Exception exception, string message);

        /// <summary>
        /// Logs information
        /// </summary>
        void Information(string message);

        /// <summary>
        /// Logs information
        /// </summary>
        void Information(Exception exception, string message);
        
        /// <summary>
        /// Logs a warning
        /// </summary>
        void Warning(string message);

        /// <summary>
        /// Logs a warning
        /// </summary>
        void Warning(Exception exception, string message);
     
        /// <summary>
        /// Logs an error
        /// </summary>
        void Error(string message);

        /// <summary>
        /// Logs an error
        /// </summary>
        void Error(Exception exception, string message);
    }
}