namespace ScannerWeb.Interfaces
{
    public interface ISerialService : IDisposable
    {
        public string COM { get; set; }
        Task Connect(CancellationToken token);
        Task Disconnect();
        Task SendCommand(string cmd);

    }
}
