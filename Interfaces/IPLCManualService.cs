using ScannerWeb.Observer;

namespace ScannerWeb.Interfaces
{
    public interface IPLCManualService
    {
        Task TriggerManual(PLCIndicatorObserver.PLCIndicatorEnum indicator, bool state);
        Task ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum indicator, bool state);
    }
}
