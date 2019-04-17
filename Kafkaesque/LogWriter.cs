using System.Threading.Tasks;

namespace Kafkaesque
{
    public class LogWriter
    {
        readonly string _directoryPath;

        internal LogWriter(string directoryPath)
        {
            _directoryPath = directoryPath;
        }

        public async Task WriteAsync(byte[] bytes)
        {
            
        }
    }
}