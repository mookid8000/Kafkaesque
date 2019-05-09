using System;
using System.Collections.Generic;
using System.Linq;

namespace Kafkaesque
{
    public class Settings
    {
        public int WriteLockAcquisitionTimeoutSeconds { get; }
        public long ApproximateMaximumFileLength { get; }
        public int NumberOfFilesToKeep { get; }

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