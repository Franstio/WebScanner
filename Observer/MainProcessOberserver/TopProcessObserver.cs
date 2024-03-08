using ScannerWeb.Interfaces;
using ScannerWeb.Models.API;
using System.Diagnostics;

namespace ScannerWeb.Observer.MainProcessOberserver
{
    public class TopProcessObserver : IProcessObserver
    {
        private IDisposable? unsubscribe;
        private IPLCManualService PLCService;
        public delegate Task UpdateStep(MainProcessModel model);
        public MainProcessModel Model { get; private set; }
        private UpdateStep? UpdateStepEvent = null;
        private static SemaphoreSlim Semaphore = new SemaphoreSlim(1);
        public TopProcessObserver(IPLCManualService PLCService, MainProcessModel model, UpdateStep updateStep)
        {
            this.PLCService = PLCService;
            Model = model;
            UpdateStepEvent += updateStep;
        }
        public void OnCompleted()
        {
            if (unsubscribe != null)
                unsubscribe.Dispose();
            unsubscribe = null;
        }
        public void Dispose()
        {
        }
        public void Subscribe(IObservable<ushort[]> observable)
        {
            unsubscribe = observable.Subscribe(this);
        }

        public void OnError(Exception error)
        {
            Trace.WriteLine(error.Message);
        }

        public async void OnNext(ushort[] value)
        {
            await Semaphore.WaitAsync();
            OnCompleted();
            if (UpdateStepEvent is null)
            {
                return;
            }
            MainProcessModel? data = null;
            switch (Model.Step)
            {
                case 1:
                    data = await Step1(Model);
                    break;
                case 2:
                    if (value[(int)PLCIndicatorObserver.PLCIndicatorEnum.TOP_SENSOR] == 0)
                        data = await Step2(Model);
                    break;
                case 3:
                    if (value[(int)PLCIndicatorObserver.PLCIndicatorEnum.TOP_SENSOR] == 1)
                        data = await Step3(Model);
                    break;
                default:
                    data = Model;
                    break;
            }
            data = data ?? Model;
            try
            {
                if (UpdateStepEvent is not null)
                    await UpdateStepEvent(data);
                UpdateStepEvent = null;
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
            Semaphore.Release();
        }
        public async Task<MainProcessModel> Step1(MainProcessModel data)
        {
            if (data.Payload.doorstatus != 1 || UpdateStepEvent is null)
                return data;
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.TOP_LOCK, true);
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.GREEN_LAMP, true);
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.YELLOW_LAMP, false);
            //buzzer here
            data.Step = 2;
            data.Instruction = "Buka Penutup Atas";
            return data;
        }
        public async Task<MainProcessModel> Step2(MainProcessModel data)
        {
            if (data.Payload.doorstatus != 1 || UpdateStepEvent is null)
                return data;
            data.Instruction = "Buang sampah dan Tutup pintu . .";
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.TOP_SENSOR, false);
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.TOP_LOCK, false);
            data.Step = 3;
            return data;
        }
        public async Task<MainProcessModel> Step3(MainProcessModel data)
        {

            if (data.Payload.doorstatus != 1 || UpdateStepEvent is null)
                return data;
            data.Instruction = "Lakukan verifikasi pada Scanner Screen";
            data.Step = 4;
            data.FinalStep = true;            
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.GREEN_LAMP, false);
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.YELLOW_LAMP, true);
            return data;
        }

    }
}
