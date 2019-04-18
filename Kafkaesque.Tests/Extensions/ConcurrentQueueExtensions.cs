using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Kafkaesque.Tests.Extensions
{
    static class ConcurrentQueueExtensions
    {
        public static async Task WaitFor<TItem>(this ConcurrentQueue<TItem> queue,
            Expression<Func<ConcurrentQueue<TItem>, bool>> completionExpression,
            Expression<Func<ConcurrentQueue<TItem>, bool>> invariantExpression = null,
            int timeoutSeconds = 5)
        {
            var completionFunction = completionExpression.Compile();
            var invariantFunction = invariantExpression?.Compile() ?? (q => true);
            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            
            using (var cancellationTokenSource = new CancellationTokenSource(timeout))
            {
                try
                {
                    while (true)
                    {
                        cancellationTokenSource.Token.ThrowIfCancellationRequested();

                        if (!invariantFunction(queue))
                        {
                            throw new AssertionException($@"Invariant 

    {invariantExpression} 

violated on queue containing {queue.Count} items while waiting for

    {completionExpression}

to be satisfied. Queue contents:

{string.Join(Environment.NewLine, queue)}");
                        }

                        if (completionFunction(queue))
                        {
                            return;
                        }

                        await Task.Delay(200, cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
                {
                    throw new AssertionException($@"Completion expression

    {completionExpression}

was not satistied within {timeout} timeout. Queue contents:

{string.Join(Environment.NewLine, queue)}");
                }
            }
        }
    }
}