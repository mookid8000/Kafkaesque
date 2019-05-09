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
        readonly Settings _settings;

        /// <summary>
        /// Creates a log directory, using the directory specified by <paramref name="directoryInfo"/> to store the files.
        /// </summary>
        public LogDirectory(DirectoryInfo directoryInfo, Settings settings = null) : this(directoryInfo?.FullName, settings)
        {
        }

        /// <summary>
        /// Creates a log directory, using the directory specified by <paramref name="directoryPath"/> to store the files.
        /// </summary>
        public LogDirectory(string directoryPath, Settings settings = null)
        {
            _directoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath), "Please pass a directory path to the log directory");
            _settings = settings ?? new Settings();

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
        public LogWriter GetWriter(CancellationToken cancellationToken = default) => new LogWriter(_directoryPath, cancellationToken, _settings);

        /// <summary>
        /// Gets a log reader.
        /// </summary>
        public LogReader GetReader() => new LogReader(_directoryPath, _settings);

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
