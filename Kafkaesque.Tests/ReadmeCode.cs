using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Kafkaesque.Tests
{
    [TestFixture]
    [Ignore("Just example code, some of the tests never exit")]
    public class ReadmeCode : KafkaesqueFixtureBase
    {
        [Test]
        public async Task Writer()
        {
            var logDirectory = new LogDirectory(@"C:\data\kafkaesque");

            // hold on to this bad boy until your application shuts down
            using (var logWriter = logDirectory.GetWriter())
            {
                await logWriter.WriteAsync(new byte[] { 1, 2, 3 });
            }
        }

        [Test]
        public async Task Reader()
        {
            var logDirectory = new LogDirectory(@"C:\data\kafkaesque");

            var logReader = logDirectory.GetReader();

            foreach (var logEvent in logReader.Read())
            {
                var bytes = logEvent.Data;

                // process the bytes here
            }
        }

        [Test]
        public async Task Reader_Cancellation()
        {
            var cancellationToken = CancelAfter(TimeSpan.FromSeconds(3));

            var logDirectory = new LogDirectory(@"C:\data\kafkaesque");

            var logReader = logDirectory.GetReader();

            foreach (var logEvent in logReader.Read(cancellationToken: cancellationToken))
            {
                var bytes = logEvent.Data;

                // process the bytes here
            }
        }

        [Test]
        public void GetPositionFromLogEvent()
        {
            var cancellationToken = CancelAfter(TimeSpan.FromSeconds(3));

            var logDirectory = new LogDirectory(@"C:\data\kafkaesque");

            var logReader = logDirectory.GetReader();

            var fileNumber = -1;    //< this assumes we haven't
            var bytePosition = -1;  //< read anything before

            foreach (var logEvent in logReader.Read(fileNumber: fileNumber, bytePosition: bytePosition, cancellationToken: cancellationToken))
            {
                var bytes = logEvent.Data;

                fileNumber = logEvent.FileNumber;
                bytePosition = logEvent.BytePosition;

                // store the file number and the byte position in your database
            }


        }
    }
}