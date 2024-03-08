namespace ScannerWeb.Interfaces
{
    public interface IArduinoService : ISerialService, IObservable<string>
    {

    }
}
