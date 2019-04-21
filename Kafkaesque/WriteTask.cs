using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kafkaesque
{
    class WriteTask
    {
        readonly TaskCompletionSource<object> _taskCompletionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        public WriteTask(byte[] data, CancellationToken cancellationToken)
        {
            cancellationToken.Register(TryCancelTask);
            Data = data;
        }

        public byte[] Data { get; }

        public Task Task => _taskCompletionSource.Task;

        public void Complete() => _taskCompletionSource.TrySetResult(null);

        public void Fail(Exception exception) => _taskCompletionSource.TrySetException(exception);

        public bool IsCancelled => Task.IsCanceled;

        void TryCancelTask() => _taskCompletionSource.TrySetCanceled();
    }
}