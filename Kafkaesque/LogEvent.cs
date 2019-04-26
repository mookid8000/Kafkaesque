namespace Kafkaesque
{
    /// <summary>
    /// Represents on single log event (in the form of a byte array) along with the position
    /// from which to resume reading in order to read the next event and on
    /// </summary>
    public struct LogEvent
    {
        /// <summary>
        /// Gets the data
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Gets the file number
        /// </summary>
        public int FileNumber { get; }

        /// <summary>
        /// Gets the position of the underlying stream
        /// </summary>
        public int BytePosition { get; }

        /// <summary>
        /// Constructs the log event
        /// </summary>
        public LogEvent(byte[] data, int fileNumber, int bytePosition)
        {
            Data = data;
            FileNumber = fileNumber;
            BytePosition = bytePosition;
        }
    }
}