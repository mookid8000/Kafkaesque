namespace Kafkaesque
{
    public class Settings
    {
        public int WriteLockAcquisitionTimeoutSeconds { get; }
        public long ApproximateMaximumFileLength { get; }

        public Settings(int writeLockAcquisitionTimeoutSeconds = 20, long approximateMaximumFileLength = 10 * 1024 * 1024)
        {
            WriteLockAcquisitionTimeoutSeconds = writeLockAcquisitionTimeoutSeconds;
            ApproximateMaximumFileLength = approximateMaximumFileLength;
        }
    }
}