namespace ScannerWeb.Observer
{
    public class Unsubscribe<T> : IDisposable
    {
        private readonly List<IObserver<T>>? Observers;
        private readonly IObserver<T>? Observer;
        public Unsubscribe(List<IObserver<T>> observers, IObserver<T> observer)
        {
            Observers = observers;
            Observer = observer;
        }

        public void Dispose()
        {
            if (Observers is not null && Observer is not null && Observers.Contains(Observer))
                Observers.Remove(Observer);
        }
    }
}
