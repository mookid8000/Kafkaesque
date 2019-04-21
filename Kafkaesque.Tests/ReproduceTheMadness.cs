using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
#pragma warning disable 1998

namespace Kafkaesque.Tests
{
    [TestFixture]
    public class ReproduceTheMadness
    {
        [Test]
        public async Task Wait()
        {
            using (new ThisIsCrazy())
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        [Test]
        public async Task DontWait()
        {
            using (var crazy = new ThisIsCrazy())
            {
                await crazy.DoStuff();
            }
        }
    }

    class ThisIsCrazy : IDisposable
    {
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        readonly Task _task;

        bool stuffDone;

        public ThisIsCrazy()
        {
            _task = Task.Run(async () => await Run());
        }

        async Task Run()
        {
            var token = _cancellationTokenSource.Token;

            try
            {
                Console.WriteLine("Working");
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("CR before start");
                }
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        Console.WriteLine(".");
                        await Task.Delay(TimeSpan.FromSeconds(1), token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        // it's alright
                        Console.WriteLine("OCE (inner)");
                    }
                }

                if (stuffDone)
                {
                    Console.WriteLine("STUFF WAS DONE");
                    await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // we're exiting
                Console.WriteLine("OCE (outer)");
            }
            finally
            {
                Console.WriteLine("DONE!");
            }
        }

        public void Dispose()
        {
            using (_cancellationTokenSource)
            {
                _cancellationTokenSource.Cancel();

                if (!_task.Wait(TimeSpan.FromSeconds(3)))
                {
                    Console.WriteLine("WTF?!?!? Task did not finish working withing 3 s timeout!");
                }
            }
        }

        public Task DoStuff()
        {
            var taskCompletionSource = new TaskCompletionSource<object>();

            try
            {
                stuffDone = true;
                return taskCompletionSource.Task;
            }
            finally
            {
                taskCompletionSource.SetResult(null);
            }
        }
    }
}