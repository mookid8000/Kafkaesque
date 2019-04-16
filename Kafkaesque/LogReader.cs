using System.Collections.Generic;

namespace Kafkaesque
{
    public class LogReader
    {
        readonly string _directoryPath;

        internal LogReader(string directoryPath)
        {
            _directoryPath = directoryPath;
        }

        public IEnumerable<LogEvent> Read(int fileNumber = -1, int bytePosition = -1)
        {
            return new List<LogEvent>();
        }
    }
}