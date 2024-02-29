namespace ScannerWeb.Services
{
    public interface ISerialService : IDisposable
    {
        Task Connect();
        Task Disconnect();
        Task SendCommand(string cmd);

    }
}
