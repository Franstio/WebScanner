namespace ScannerWeb.Interfaces
{
    public interface IArduinoService : ISerialService, IObservable<string>
    {
        Task StartListening(CancellationTokenSource token);
        Task CloseConnection();
        string GetConnectionStatus();

    }
}
