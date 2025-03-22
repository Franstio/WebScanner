namespace ScannerWeb.Interfaces
{
    public interface IArduinoService : ISerialService, IObservable<string>
    {
        Task StartListening(CancellationToken token);
        Task CloseConnection();
        string GetConnectionStatus();

    }
}
