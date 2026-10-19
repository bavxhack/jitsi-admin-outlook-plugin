using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using JitsiAdmin.OutlookPlugin.Core;

namespace JitsiAdmin.OutlookPlugin
{
    internal sealed class UiDispatcher : IUiDispatcher, IDisposable
    {
        private readonly Control control = new Control();
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        public UiDispatcher()
        {
            var handle = control.Handle;
            if (SynchronizationContext.Current == null || SynchronizationContext.Current.GetType() == typeof(SynchronizationContext))
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        }
        public void Verify() { if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Outlook UI thread required."); }
        public Task<T> InvokeAsync<T>(Func<T> action)
        {
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Post(() => { try { completion.TrySetResult(action()); } catch (Exception e) { completion.TrySetException(e); } },
                e => completion.TrySetException(e));
            return completion.Task;
        }
        public Task InvokeAsync(Func<Task> action)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Post(async () => { try { await action(); completion.TrySetResult(true); } catch (Exception e) { completion.TrySetException(e); } },
                e => completion.TrySetException(e));
            return completion.Task;
        }
        private void Post(Action action, Action<Exception> fail)
        {
            try
            {
                if (control.IsDisposed) throw new ObjectDisposedException(nameof(UiDispatcher));
                if (Thread.CurrentThread.ManagedThreadId == thread) action(); else control.BeginInvoke(action);
            }
            catch (Exception e) { fail(e); }
        }
        public void Dispose() { Verify(); control.Dispose(); }
    }
}
