using System;
using System.IO;
using System.Threading;
using Serilog;
using Testy;
using Testy.Files;

namespace Kafkaesque.Tests
{
    public abstract class KafkaesqueFixtureBase : FixtureBase
    {
        static KafkaesqueFixtureBase()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .MinimumLevel.Verbose()
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
    }
}