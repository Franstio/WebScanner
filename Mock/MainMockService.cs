using Microsoft.Extensions.Options;
using ScannerWeb.Interfaces;
using ScannerWeb.Models;
using ScannerWeb.Models.API;
using ScannerWeb.Observer;
using ScannerWeb.Observer.MainProcessOberserver;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ScannerWeb.Mock
{
    public class MainMockService : IMainService
    {
        private readonly IArduinoService _arduinoService;
        private readonly IPLCService _plcService;
        private readonly ConfigModel config;
        private readonly ArduinoWeightObserver arduinoWeightObserver;
        public bool isServerAlive { get; private set; } = false;
        public string? HostName { private get; set; } = null;
        private List<IObserver<bool>> Observers = new List<IObserver<bool>>();
        private List<IObserver<string>> ObserverInstruction = new List<IObserver<string>>();

        private decimal GetPrevWeight()
        {
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.log")))
                SetPrevWeight(0);
            using (FileStream fs = new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.log"), FileMode.Open))
            {
                using (StreamReader rd = new StreamReader(fs))
                {
                    char[] buffer = new char[18];
                    rd.Read(buffer, 0, buffer.Length);
                    string text = new string(buffer).Replace("\0","").Replace("\n","");
                    logger.LogInformation(text);
                    decimal _val;
                    bool s = decimal.TryParse(text, out _val);
                    return s ? _val : 0;
                }
            }
        }
        private void SetPrevWeight(decimal value)
        {
            using (FileStream fs = new FileStream(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.log"), FileMode.Create))
            {

                using (StreamWriter wr = new StreamWriter(fs))
                {
                    byte[] data = Encoding.ASCII.GetBytes(value.ToString());
                    fs.Write(data, 0, data.Length);
                    fs.Flush();
                }
            }
        }
        private decimal CurrentWeight = decimal.Zero;
        private bool isRunning = false;
        private bool isLocked = false;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private static SemaphoreSlim sem = new SemaphoreSlim(1);
        private ILogger<MainMockService> logger;
        public MainMockService(IArduinoService arduinoService, IPLCService plcService, IOptions<ConfigModel> options, ILogger<MainMockService> logger)
        {
            _arduinoService = arduinoService;
            _plcService = plcService;
            config = options.Value;
            arduinoWeightObserver = new ArduinoWeightObserver();
            arduinoWeightObserver.WeightReceivedEvent += WeightReceived;
            arduinoWeightObserver.Subscribe(arduinoService);
            this.logger = logger;
        }
        private async Task StepUpdated(MainProcessModel step)
        {

            logger.LogInformation("Move Step " + step.Step);
            if (step.FinalStep)
            {
                try
                {
                    if (step.Type == MainProcessModel.ProcessType.Top)
                        _plcService.FinishObserver<TopProcessObserver>();
                    else if (step.Type == MainProcessModel.ProcessType.Bottom)
                        _plcService.FinishObserver<BottomProcessObserver>();
                    await sem.WaitAsync();
                    NotifyInstruction("Lakukan verifikasi pada Scanner Screen");
                    decimal dataKg = CurrentWeight;
                    decimal finalWeight = step.Type == MainProcessModel.ProcessType.Top ? dataKg - GetPrevWeight() : 0;
                    string activity = step.Type == MainProcessModel.ProcessType.Top ? "Dispose" : "Collection";
                    var payload = BuildProcessFinalPayload(step, finalWeight, activity);
                    await _plcService.TriggerManual(PLCIndicatorObserver.PLCIndicatorEnum.GREEN_LAMP, false);
                    SetPrevWeight(dataKg);
                    isRunning = false;
                    isLocked = false;
                    StartMonitor();
                    sem.Release();
                }
                catch (Exception ex)
                {
                    logger.LogError($"Final Step {step.Type}: {ex.Message}");
                    if (isLocked)
                        isLocked = false;
                }
            }
            else
            {
                IProcessObserver observer = step.Type == MainProcessModel.ProcessType.Top ? new TopProcessObserver(_plcService, step, StepUpdated) : new BottomProcessObserver(_plcService, step, StepUpdated,_plcService);
                NotifyInstruction(step.Instruction);
                observer.Subscribe(_plcService);
            }


        }
        private object BuildProcessFinalPayload(MainProcessModel step, decimal finalWeight, string _activity)
        {
            string stationName = HostName ?? Environment.MachineName;
            stationName = stationName.Substring(0, stationName.Length - 3);
            return new
            {
                badgeno = step.Payload.lastbadgeno,
                logindate = "",
                stationname = stationName,
                frombinname = step.Type == MainProcessModel.ProcessType.Top ? step.Payload.lastfrombinname :  HostName ?? Environment.MachineName,
                tobinname = step.Type == MainProcessModel.ProcessType.Top ? HostName ?? Environment.MachineName : string.Empty,
                weight = finalWeight,
                activity = _activity
            };
        }
        private async Task WeightReceived(decimal _weight)
        {
            CurrentWeight = _weight;
            try
            {
                isServerAlive = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                isServerAlive = false;
            }
            finally
            {
                Notify();
                await Task.Delay(500);
            }

        }
        private void CleanObserversServerAlive()
        {
            var obs = new List<IObserver<bool>>();
            for (int i = 0; i < Observers.Count; i++)
                if (Observers[i] is not null)
                    obs.Add(Observers[i]);
            Observers = obs;
        }
        private void Notify()
        {
            for (int i = 0; i < Observers.Count; i++)
                Observers[i].OnNext(isServerAlive);
            //CleanObserversServerAlive();
        }
        private void NotifyInstruction(string message)
        {
            logger.LogInformation("Notify " + message);
            for (int i = 0; i < ObserverInstruction.Count; i++)
                if (ObserverInstruction[i] is not null)
                    ObserverInstruction[i].OnNext(message);
            //CleanObserversInstrcution();
        }
        private void CleanObserversInstrcution()
        {
            var obs = new List<IObserver<string>>();
            for (int i = 0; i < ObserverInstruction.Count; i++)
                if (ObserverInstruction[i] is not null)
                    obs.Add(ObserverInstruction[i]);
            ObserverInstruction = obs;
        }
        public void StartMonitor()
        {
            Task.Run(async delegate { await MonitorAPI(cts.Token); });
        }
        public void CancelMonitor()
        {
            if (isRunning)
            {
                cts.Cancel();
                cts.Dispose();
                cts = new CancellationTokenSource();
                isRunning = false;
                isLocked = false;
                _plcService.FinishObserver<TopProcessObserver>();
                _plcService.FinishObserver<BottomProcessObserver>();
            }
        }
        private async Task MonitorAPI(CancellationToken token)
        {
            if (isRunning || isLocked)
                return;
            while (!token.IsCancellationRequested)
            {
                isRunning = true;
                try
                {
                    var payload = new
                    {
                        Object = "mwastebin",
                        where = $"name={(string.IsNullOrEmpty(HostName) ? Environment.MachineName : HostName)}"
                    };
                    //                        var response = await client.PostAsJsonAsync(config.ReadStatusEndPoint, payload);


                    APIPayloadModel? model = new APIPayloadModel()
                    {
                        data = new APIPayloadModel.APIPayloadItemModel[]{
                                new()
                                {
                                    doorstatus=2,
                                    lastbadgeno="test",
                                    lastfrombinname="2-B1-001"
                                },
                            }
                    };
                    if (model != null && model.data.Length > 0)
                    {
                        logger.LogInformation("Move Step 1");
                        MainProcessModel processModel = new MainProcessModel()
                        {
                            Payload = model.data[0],
                            Step = 1,
                            Type = model.data[0].doorstatus == 1 ? MainProcessModel.ProcessType.Top : MainProcessModel.ProcessType.Bottom,
                            FinalStep = false
                        };
                        logger.LogInformation(JsonSerializer.Serialize(processModel));
                        if (model.data[0].doorstatus is null || (model.data[0].doorstatus != 1 && model.data[0].doorstatus != 2))
                            continue;
                        isLocked = true;
                        await StepUpdated(processModel);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError("Monitor Error: " + ex.Message);

                }
            }
        }

        public IDisposable Subscribe(IObserver<bool> observer)
        {
            if (!Observers.Contains(observer))
                Observers.Add(observer);
            return new Unsubscribe<bool>(Observers, observer);
        }
        public void Dispose()
        {
            arduinoWeightObserver.WeightReceivedEvent -= WeightReceived;
            for (int i = 0; i < Observers.Count; i++)
                Observers[i].OnCompleted();

        }

        public IDisposable Subscribe(IObserver<string> observer)
        {
            if (!ObserverInstruction.Contains(observer))
                ObserverInstruction.Add(observer);
            return new Unsubscribe<string>(ObserverInstruction, observer);
        }
    }
}
