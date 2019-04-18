using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Serilog;
using Serilog.Events;

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class TestLogWriter : KafkaesqueFixtureBase
    {
        [TestCase(true, 10)]
        [TestCase(false, 10)]
        [TestCase(true, 100)]
        [TestCase(false, 100)]
        [TestCase(true, 1000)]
        [TestCase(false, 1000)]
        public async Task CompareSingleVersusMany(bool single, int count)
        {
            SetLogLevel(LogEventLevel.Information);

            var messages = Enumerable.Range(0, count).Select(n => $"THIS IS A STRING MESSAGE EVENT/{n}").ToList();
            var logDirectory = new LogDirectory(GetLogDirectoryPath());

            var stopwatch = Stopwatch.StartNew();

            using (var writer = logDirectory.GetWriter())
            {
                Log.Information("Writing");
                if (single)
                {
                    foreach (var message in messages)
                    {
                        await writer.WriteAsync(Encoding.UTF8.GetBytes(message));
                    }
                }
                else
                {
                    await writer.WriteManyAsync(messages.Select(Encoding.UTF8.GetBytes));
                }
                Log.Information("Done writing!");
            }

            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

            Console.WriteLine($"Wrote {count} msgs in {elapsedSeconds:0.0} s - that's {count/elapsedSeconds:0.0} msg/s");
        }

        [Test]
        public async Task CanWriteAndReadItBack()
        {
            var logDirectoryPath = GetLogDirectoryPath();
            var logDirectory = new LogDirectory(logDirectoryPath);

            var logWriter = logDirectory.GetWriter();

            Using(logWriter);

            await logWriter.WriteAsync(new byte[] { 1, 2, 3 }, CancelAfter(TimeSpan.FromSeconds(3)));

            var reader = logDirectory.GetReader();

            var logEvents = reader.Read().ToList();

            Assert.That(logEvents.Count, Is.EqualTo(1));

            var logEvent = logEvents.First();

            Assert.That(logEvent.Data, Is.EqualTo(new byte[] { 1, 2, 3 }));
        }

        [TestCase(100, false)]
        [TestCase(1000, false)]
        [TestCase(10000, false)]
        [TestCase(100, true)]
        [TestCase(1000, true)]
        [TestCase(10000, true)]
        public async Task WhatHappensIfWeWriteALot(int iterations, bool parallel)
        {
            SetLogLevel(LogEventLevel.Information);

            var logDirectoryPath = GetLogDirectoryPath();
            var logDirectory = new LogDirectory(logDirectoryPath);

            var logWriter = logDirectory.GetWriter();

            Using(logWriter);

            var bytes = Enumerable.Range(0, 20000)
                .Select(o => (byte)(iterations % 256))
                .ToArray();

            var stopwatch = Stopwatch.StartNew();

            if (parallel)
            {
                await Task.WhenAll(Enumerable.Range(0, iterations)
                    .Select(i => logWriter.WriteAsync(bytes)));
            }
            else
            {
                for (var counter = 0; counter < iterations; counter++)
                {
                    await logWriter.WriteAsync(bytes);
                }
            }

            var files = Directory.GetFiles(logDirectoryPath)
                .Select(file => new {FilePath = file, Length = new FileInfo(file).Length})
                .ToList();

            Console.WriteLine($@"Here are the files:

{string.Join(Environment.NewLine, files.Select(f => $"    {f.FilePath} ({FormatSize(f.Length)})"))}

");

            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            var totalBytesWritten = files.Sum(a => a.Length);

            Console.WriteLine($"Wrote {FormatSize(totalBytesWritten)} in {elapsedSeconds:0.0} s - that's {FormatSize((long)(totalBytesWritten/elapsedSeconds))}/s");
        }

        static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";

            var kiloBytes = bytes / (double)1024;

            if (kiloBytes < 1024) return $"{kiloBytes:0.0#} kB";

            var megaBytes = kiloBytes / 1024;

            return $"{megaBytes:0.0#} MB";
        }
    }
}