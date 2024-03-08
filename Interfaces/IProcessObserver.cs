namespace ScannerWeb.Interfaces
{
    public interface IProcessObserver : IObserver<ushort[]>, IDisposable
    {
        public void Subscribe(IObservable<ushort[]> observable);
    }
}
