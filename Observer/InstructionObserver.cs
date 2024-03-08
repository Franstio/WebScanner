using System.Diagnostics;

namespace ScannerWeb.Observer
{
    public class InstructionObserver : IObserver<string>
    {
        public IDisposable? unsubscribe = null;
        public delegate Task UpdateInstruction(string msg);
        public UpdateInstruction? UpdateInstructionEvent {  get; set; } = null;
        public void OnCompleted()
        {
            if (unsubscribe != null)
                unsubscribe.Dispose();
        }

        public void OnError(Exception error)
        {
            Trace.WriteLine(error.Message);
        }

        public async void OnNext(string value)
        {
            if (UpdateInstructionEvent is not null)
                await UpdateInstructionEvent(value);
        }
        public void Subscribe(IObservable<string> observable)
        {
            unsubscribe = observable.Subscribe(this);
        }
        public void Unsubscribe()
        {
            OnCompleted();
        }
    }
}
