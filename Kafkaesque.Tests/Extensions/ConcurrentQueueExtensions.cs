﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Kafkaesque.Tests.Extensions
{
    static class ConcurrentQueueExtensions
    {
        public static ConcurrentQueue<TItem> ToConcurrentQueue<TItem>(this IEnumerable<TItem> items) =>
            new ConcurrentQueue<TItem>(items);

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
                            var list = queue.ToList();

                            const int maxListLength = 100;

                            var itemsToPrint = list.Count > maxListLength
                                ? list.Take(maxListLength/2).Concat(list.Skip(list.Count - maxListLength/2)).ToList()
                                : list.ToList();

                            var listWasShortened = itemsToPrint.Count < list.Count;

                            throw new ArgumentException($@"Invariant 

    {invariantExpression} 

violated on queue containing {queue.Count} items while waiting for

    {completionExpression}

to be satisfied. Queue contents{(listWasShortened ? $" (shortened from {list.Count} items):" : ":")}

{string.Join(Environment.NewLine, itemsToPrint)}");
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
                    var list = queue.ToList();

                    const int maxListLength = 100;

                    var itemsToPrint = list.Count > maxListLength
                        ? list.Take(maxListLength/2).Concat(list.Skip(list.Count - maxListLength/2)).ToList()
                        : list.ToList();

                    var listWasShortened = itemsToPrint.Count < list.Count;

                    throw new TimeoutException($@"Completion expression

    {completionExpression}

was not satistied within {timeout} timeout. Queue contents{(listWasShortened ? $" (shortened from {list.Count} items):" : ":")}

{string.Join(Environment.NewLine, queue)}");
                }
            }
        }
    }
}