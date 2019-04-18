using System;
using System.IO;
using System.Threading;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Testy;
using Testy.Files;

namespace Kafkaesque.Tests
{
    public abstract class KafkaesqueFixtureBase : FixtureBase
    {
        static readonly LoggingLevelSwitch LoggingLevelSwitch = new LoggingLevelSwitch();

        static KafkaesqueFixtureBase()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .MinimumLevel.ControlledBy(LoggingLevelSwitch)
                .CreateLogger();

            AppDomain.CurrentDomain.DomainUnload += (o, ea) => Log.CloseAndFlush();
        }

        protected CancellationToken CancelAfter(TimeSpan delay)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(delay);
            return cancellationTokenSource.Token;
        }

        protected string GetLogDirectoryPath() => Path.Combine(Using(new TemporaryTestDirectory()), "log");

        protected KafkaesqueFixtureBase() => SetLogLevel(LogEventLevel.Verbose);

        protected void SetLogLevel(LogEventLevel level) => LoggingLevelSwitch.MinimumLevel = level;
    }
}