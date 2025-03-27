namespace ScannerWeb.Interfaces
{
    public interface ISerialService : IDisposable
    {
        public string COM { get; set; }
        Task Connect(CancellationTokenSource token);
        Task Disconnect();
        Task SendCommand(string cmd);

    }
}
