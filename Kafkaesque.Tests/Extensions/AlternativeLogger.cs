using System;
using Serilog;

namespace Kafkaesque.Tests.Extensions
{
    public class AlternativeLogger : IDisposable
    {
        readonly ILogger _previousLogger = Log.Logger;

        public AlternativeLogger(Action<LoggerConfiguration> configure)
        {
            var configuration = new LoggerConfiguration();
            configure(configuration);
            Log.Logger = configuration.CreateLogger();          
        }

        public void Dispose() => Log.Logger = _previousLogger;
    }
}