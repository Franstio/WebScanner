using ScannerWeb.Observer;

namespace ScannerWeb.Interfaces
{
    public interface IPLCService : ISerialService, IPLCManualService, IObservable<ushort[]>,IObservable<bool>,ILockButtonUpdate
    {
        Task StartReadingInput(CancellationToken token, ushort count);
        Task SendCommand(ushort address, ushort value);
        Task<ushort[]?> ReadCommand(ushort address, ushort numberOfPoint);
        Task TriggerManual(PLCIndicatorObserver.PLCIndicatorEnum indicator, bool state);
        void FinishObserver<T>();
    }
}
