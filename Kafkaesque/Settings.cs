using System;
using System.Collections.Generic;
using System.Linq;

namespace Kafkaesque
{
    /// <summary>
    /// Configures how Kafkaesque should do its thing
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// Gets how long (in seconds) the writer should accept waiting to acquire the write lock.
        /// </summary>
        public int WriteLockAcquisitionTimeoutSeconds { get; }

        /// <summary>
        /// Gets the approximate size (in bytes) of how big files can get. This is an approximate value, as contents will flow into a new file
        /// whenever this value has been crossed, but – depending on the size of the written chunk of data – the file can end up slightly bigger
        /// than this limit.
        /// </summary>
        public long ApproximateMaximumFileLength { get; }

        /// <summary>
        /// Gets how many files Kafkaesque should aim to keep as history in the log directory.
        /// </summary>
        public int NumberOfFilesToKeep { get; }

        /// <summary>
        /// Creates the settings
        /// </summary>
        public Settings(
            int writeLockAcquisitionTimeoutSeconds = 20,
            long approximateMaximumFileLength = 10 * 1024 * 1024,
            int numberOfFilesToKeep = 100
        )
        {
            WriteLockAcquisitionTimeoutSeconds = writeLockAcquisitionTimeoutSeconds;
            ApproximateMaximumFileLength = approximateMaximumFileLength;
            NumberOfFilesToKeep = numberOfFilesToKeep;
        }

        internal void Validate()
        {
            var errors = new List<string>();

            if (WriteLockAcquisitionTimeoutSeconds < 0)
            {
                errors.Add($"Write lock acquisition timeout {WriteLockAcquisitionTimeoutSeconds} cannot be used – it must be 0 or greater");
            }

            if (ApproximateMaximumFileLength < 1024)
            {
                errors.Add($"Approx. max. file length of {ApproximateMaximumFileLength} bytes is fewer than the 1024 byte limit");
            }

            if (NumberOfFilesToKeep < 5)
            {
                errors.Add($"Number of files to keep = {NumberOfFilesToKeep} is less than the minimum number of 5");
            }

            if (!errors.Any()) return;

            throw new ArgumentException($@"Could not initialize LogDirectory because of the following errors:

{ string.Join(Environment.NewLine + Environment.NewLine, errors)}");
        }
    }
}