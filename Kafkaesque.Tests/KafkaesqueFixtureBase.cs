using System;
using System.Threading;
using Serilog;
using Testy;

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
        }

        protected CancellationToken CancelAfter(TimeSpan delay)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(delay);
            return cancellationTokenSource.Token;
        }
    }
}