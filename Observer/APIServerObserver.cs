using System.Diagnostics;

namespace ScannerWeb.Observer
{
    public class APIServerObserver : IObserver<bool>
    {
        private IDisposable? unsubscribe;
        public UpdateValue? UpdateEvent { get; set; }
        public void OnCompleted()
        {
            if (unsubscribe is not null)
                unsubscribe.Dispose();
        }
        public void Subscribe(IObservable<bool> observer)
        {
            unsubscribe = observer.Subscribe(this);
            
        }
        public void OnError(Exception error)
        {
            Trace.WriteLine($"Error From Api Server Observer: {error.Message}");
        }

        public async void OnNext(bool value)
        {
            if (UpdateEvent is not null)
                await UpdateEvent(value);
        }
        public delegate Task UpdateValue(bool value);
    }
}
