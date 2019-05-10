using System;
using Serilog;

namespace Kafkaesque.Tests.Extensions
{
    class SerilogLogger : ILogger
    {
        readonly Serilog.ILogger _logger;

        public SerilogLogger() : this(Log.Logger)
        {
        }

        public SerilogLogger(Serilog.ILogger logger) => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public void Verbose(string message)
        {
            _logger.Verbose(message);
        }

        public void Verbose(Exception exception, string message)
        {
            _logger.Verbose(exception, message);
        }

        public void Information(string message)
        {
            _logger.Information(message);
        }

        public void Information(Exception exception, string message)
        {
            _logger.Information(exception, message);
        }

        public void Warning(string message)
        {
            _logger.Warning(message);
        }

        public void Warning(Exception exception, string message)
        {
            _logger.Warning(exception, message);
        }

        public void Error(string message)
        {
            _logger.Error(message);
        }

        public void Error(Exception exception, string message)
        {
            _logger.Error(exception, message);
        }
    }
}