using ScannerWeb.Interfaces;
using ScannerWeb.Models.API;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ScannerWeb.Observer.MainProcessOberserver
{
    public class BottomProcessObserver : IProcessObserver
    {
        private IDisposable? unsubscribe;
        private IPLCManualService PLCService;
        public delegate Task UpdateStep(MainProcessModel model);
        public MainProcessModel Model { get; private set; }
        public UpdateStep? UpdateStepEvent { private get; set; } = null;
        private static SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1);
        private ILockButtonUpdate iLockButtonUpdate;
        public BottomProcessObserver(IPLCManualService PLCService, MainProcessModel model, UpdateStep updateStep, ILockButtonUpdate iLockButtonUpdate)
        {
            this.PLCService = PLCService;
            Model = model;
            UpdateStepEvent += updateStep;
            this.iLockButtonUpdate = iLockButtonUpdate;
        }
        public void OnCompleted()
        {
            if (unsubscribe != null)
                unsubscribe.Dispose();
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
            await SemaphoreSlim.WaitAsync();
            OnCompleted();
            MainProcessModel? data = null;
            switch (Model.Step)
            {
                case 1:
                    data = await Step1(Model);
                    break;
                case 2:
                    if (value[(int)PLCIndicatorObserver.PLCIndicatorEnum.BOTTOM_SENSOR] == 0)
                        data = await Step2(Model);
                    break;
                case 3:
                    if (value[(int)PLCIndicatorObserver.PLCIndicatorEnum.BOTTOM_SENSOR] == 1)
                        data = await Step3(Model);
                    break; 
                case 4:
                    if (value[(int)PLCIndicatorObserver.PLCIndicatorEnum.BOTTOM_LOCK]==1)
                        data = await Step4(Model);
                    break;
                case 5:
                    if (value[(int)PLCIndicatorObserver.PLCIndicatorEnum.BOTTOM_SENSOR] == 0)
                        data = await Step5(Model);
                    break;
                case 6:
                    if (value[(int)PLCIndicatorObserver.PLCIndicatorEnum.BOTTOM_SENSOR] == 1)
                        data = await Step6(Model);
                    break;
                default:
                    data = Model; break;
            }
            data = data ?? Model;
            try
            {
                if (UpdateStepEvent is not null)
                    await UpdateStepEvent(data);
                UpdateStepEvent = null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
            SemaphoreSlim.Release();
        }
        public async Task<MainProcessModel> Step1(MainProcessModel data)
        {
            if (data.Payload.doorstatus != 2 || UpdateStepEvent is null)
                return data;
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.BOTTOM_LOCK, true);
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.GREEN_LAMP, true);
            
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.YELLOW_LAMP, false);
            //buzzer here
            data.Step = 2;
            data.Instruction = "Buka Penutup Bawah ";
            return data;
        }
        public async Task<MainProcessModel> Step2(MainProcessModel data)
        {

            if (data.Payload.doorstatus != 2 || UpdateStepEvent is null)
                return data;
            data.Step = 3;
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.BOTTOM_LOCK, false);
            data.Instruction = "Keluarkan Bin";
            return data;
        }
        public async Task<MainProcessModel> Step3(MainProcessModel data)
        {
            if (data.Payload.doorstatus != 2 || UpdateStepEvent is null)
                return data;
            data.Instruction = " Tekan Lock untuk membuka container";
            iLockButtonUpdate.UpdateLockState(false);
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.BOTTOM_LOCK, false);
            data.Step = 4;
            return data;
        }
        public async Task<MainProcessModel> Step4(MainProcessModel data)
        {

            if (data.Payload.doorstatus != 2 || UpdateStepEvent is null)
                return data;
            iLockButtonUpdate.UpdateLockState(true);
            data.Instruction = "Buka Penutup bawah dan masukkan Bin";
            data.Step = 5;
            await UpdateStepEvent(data);
            return data;
        }
        public async Task<MainProcessModel> Step5(MainProcessModel data)
        {

            if (data.Payload.doorstatus != 2 || UpdateStepEvent is null)
                return data;
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.BOTTOM_LOCK, false);
            data.Instruction = "Tutup punutup bawah";
            data.Step = 6;
            return data;
        }
        public async  Task<MainProcessModel> Step6(MainProcessModel data)
        {

            if (data.Payload.doorstatus != 2 || UpdateStepEvent is null)
                return data;
            data.Instruction = "Lakukan verifikasi pada Scanner Screen";
            data.Step = -1;
            data.FinalStep = true;
            
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.GREEN_LAMP, false);
            
            await PLCService.ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum.YELLOW_LAMP, true);
            return data;
        }

    }
}
