using System;

namespace Kafkaesque.Internals
{
    class NullLogger : ILogger
    {
        public void Verbose(string message)
        {
        }

        public void Verbose(Exception exception, string message)
        {
        }

        public void Information(string message)
        {
        }

        public void Information(Exception exception, string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Warning(Exception exception, string message)
        {
        }

        public void Error(string message)
        {
        }

        public void Error(Exception exception, string message)
        {
        }
    }
}