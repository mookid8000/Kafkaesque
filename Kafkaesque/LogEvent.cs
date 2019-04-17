namespace Kafkaesque
{
    public struct LogEvent
    {
        public byte[] Data { get; }
        public int FileNumber { get; }
        public int BytePosition { get; }

        public LogEvent(byte[] data, int fileNumber, int bytePosition)
        {
            Data = data;
            FileNumber = fileNumber;
            BytePosition = bytePosition;
        }
    }
}