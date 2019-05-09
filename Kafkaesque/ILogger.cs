using System;

namespace Kafkaesque
{
    public interface ILogger
    {
        void Verbose(string message);
        void Verbose(Exception exception, string message);

        void Information(string message);
        void Information(Exception exception, string message);
        
        void Warning(string message);
        void Warning(Exception exception, string message);
     
        void Error(string message);
        void Error(Exception exception, string message);
    }
}