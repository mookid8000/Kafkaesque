using System;
using System.IO;
using System.Threading;
// ReSharper disable SuggestBaseTypeForParameter

namespace Kafkaesque
{
    /// <summary>
    /// Create an instance of this one to get started with Kafkaesque
    /// </summary>
    public class LogDirectory
    {
        readonly string _directoryPath;

        /// <summary>
        /// Creates a log directory, using the directory specified by <paramref name="directoryInfo"/> to store the files.
        /// </summary>
        public LogDirectory(DirectoryInfo directoryInfo) : this(directoryInfo?.FullName)
        {
        }

        /// <summary>
        /// Creates a log directory, using the directory specified by <paramref name="directoryPath"/> to store the files.
        /// </summary>
        public LogDirectory(string directoryPath)
        {
            _directoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath), "Please pass a directory path to the log directory");

            if (!Directory.Exists(directoryPath))
            {
                CreateLogDirectory(directoryPath);
            }
        }

        /// <summary>
        /// Gets a log writer. This requires exclusive access to the log directory, so it will acquire a file-based lock on it.
        /// If the lock cannot be acquired within 20 s, a <see cref="TimeoutException"/> is thrown.
        /// Optionally pass a <paramref name="cancellationToken"/> if you want to be able to abort the acquisition attempt
        /// prematurely.
        /// </summary>
        public LogWriter GetWriter(CancellationToken cancellationToken = default) => new LogWriter(_directoryPath, cancellationToken);

        /// <summary>
        /// Gets a log reader.
        /// </summary>
        public LogReader GetReader() => new LogReader(_directoryPath);

        static void CreateLogDirectory(string directoryPath)
        {
            try
            {
                Directory.CreateDirectory(directoryPath);
            }
            catch (Exception exception)
            {
                if (Directory.Exists(directoryPath)) return;

                throw new IOException($"Could not create directory '{directoryPath}'", exception);
            }
        }
    }
}
