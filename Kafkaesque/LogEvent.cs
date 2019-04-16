namespace Kafkaesque
{
    public struct LogEvent
    {
        public string FilePath { get; }
        public byte[] Data { get; }
        public int FileNumber { get; }
        public int BytePosition { get; }

        public LogEvent(string filePath, byte[] data, int fileNumber, int bytePosition)
        {
            FilePath = filePath;
            Data = data;
            FileNumber = fileNumber;
            BytePosition = bytePosition;
        }
    }
}