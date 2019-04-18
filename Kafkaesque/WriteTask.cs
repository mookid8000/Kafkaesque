using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kafkaesque
{
    class WriteTask
    {
        readonly TaskCompletionSource<object> _taskCompletionSource = new TaskCompletionSource<object>();

        public WriteTask(byte[] data, CancellationToken cancellationToken)
        {
            cancellationToken.Register(TryCancelTask);
            Data = data;
        }

        public byte[] Data { get; }

        public Task Task => _taskCompletionSource.Task;

        public void Complete() => _taskCompletionSource.SetResult(null);

        public void Fail(Exception exception) => _taskCompletionSource.SetException(exception);

        public bool IsCancelled => Task.IsCanceled;

        void TryCancelTask()
        {
            try
            {
                _taskCompletionSource.SetCanceled();
            }
            catch
            {
            }
        }
    }
}