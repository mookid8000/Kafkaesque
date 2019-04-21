using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kafkaesque;
using Serilog;

namespace KafkaesqueConsoleApp
{
    class Program
    {
        static async Task Main()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .MinimumLevel.Verbose()
                .CreateLogger();

            var count = 100;
            var logDirectory = new LogDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"));
            var stopwatch = Stopwatch.StartNew();

            using (var writer = logDirectory.GetWriter())
            {
                var messages = Enumerable.Range(0, count).Select(n => $"THIS IS MESSAGE NUMBER {n}");

                await writer.WriteManyAsync(messages.Select(Encoding.UTF8.GetBytes));

                //await Task.Delay(TimeSpan.FromSeconds(.1));
            }

            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

            Console.WriteLine($"Wrote {count} messages in {elapsedSeconds:0.0} s - that's {count/elapsedSeconds:0.0} msg/s");
        }
    }
}
