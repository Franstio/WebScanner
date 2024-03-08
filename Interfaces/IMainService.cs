namespace ScannerWeb.Interfaces
{
    public interface IMainService : IObservable<bool>, IObservable<string>, IDisposable
    {
        void StartMonitor();
        void CancelMonitor();
    }
}
