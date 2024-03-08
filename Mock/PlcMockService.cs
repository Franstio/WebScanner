using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using Microsoft.Extensions.Options;
using NModbus;
using ScannerWeb.Interfaces;
using ScannerWeb.Models;
using ScannerWeb.Observer;
using System.Diagnostics;
using System.IO.Ports;
using static ScannerWeb.Observer.PLCIndicatorObserver;

namespace ScannerWeb.Mock
{
    public class PlcMockService : IPLCService
    {
        private List<IObserver<ushort[]>> Observers = new List<IObserver<ushort[]>>();
        private List<IObserver<bool>> LockUpdateObserver = new List<IObserver<bool>>();
        public string COM { get; set; } = "/dev/ttyUSB1";
        public byte SlaveId { get; set; } = 1;
        private bool isRunning = false;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private Dictionary<ushort,MainStatusModel> Mains = new Dictionary<ushort, MainStatusModel>(
        new List<KeyValuePair<ushort,MainStatusModel>>()  
        {

            new KeyValuePair<ushort,MainStatusModel>( 0, new MainStatusModel(" TOP SENSOR")),
            new KeyValuePair<ushort, MainStatusModel>(1,new MainStatusModel("BOTTOM SENSOR")),
            new KeyValuePair<ushort, MainStatusModel>(4,new MainStatusModel("TOP LOCK")),
            new KeyValuePair<ushort, MainStatusModel>(5,new MainStatusModel("BOTTOM LOCK")),
            new KeyValuePair<ushort, MainStatusModel>(6,new MainStatusModel("GREEN LAMP")),
            new KeyValuePair<ushort, MainStatusModel>(7,new MainStatusModel("YELLOW LAMP")),
            new KeyValuePair<ushort, MainStatusModel> (8,new MainStatusModel("RED LAMP"))
        });
        public PlcMockService(IOptions<ConfigModel> opt)
        {
            COM = opt.Value.PlcCOM;
        }
        public Task Connect(CancellationToken ctoken)
        {
            cts = new CancellationTokenSource();
            return Task.CompletedTask;
        }
        private void CleanObservers()
        {
            var obs = new List<IObserver<ushort[]>>();
            for (int i=0;i<Observers.Count; i++)
                if (Observers[i] is not null)
                    obs.Add(Observers[i]);
            Observers = obs;
        }
        public async Task TriggerManual(PLCIndicatorObserver.PLCIndicatorEnum indicator, bool state)
        {
            await SendCommand((byte)indicator, state ? (ushort)0 : (ushort)1);
        }
        public async Task StartReadingInput(CancellationToken token, ushort count)
        {
            if (COM == string.Empty || isRunning)
                return;
            Trace.WriteLine("Start Reading PLC");
            isRunning = true;
            try
            {
                while (!token.IsCancellationRequested && !cts.Token.IsCancellationRequested)
                {
                    try
                    {

                        ushort[]? data = await ReadCommand(0, 10);
                        Trace.WriteLine(String.Join(",", data));
                        if (data is null)
                            continue;
                        for (int i = 0; i < Observers.Count; i++)
                            if (Observers[i] != null)
                                Observers[i].OnNext(data);
                        //CleanObservers();
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
            finally
            {
                isRunning = false;
            }
        }

        public Task Disconnect()
        {
            cts.Cancel();
            cts.Dispose();
            cts= new CancellationTokenSource();
            isRunning = false;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Disconnect();
        }

        public Task SendCommand(string cmd)
        {
            throw new NotImplementedException("Please use SendCommand(ushort addres, ushort value) for PLC Service");
        }
        public  Task SendCommand(ushort address, ushort value)
        {

            try
            {
                Mains[address].Status = value == 1;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
            return Task.CompletedTask;
        }
        public  Task<ushort[]?> ReadCommand(ushort address, ushort numberOfPoint)
        {
            try
            {
                ushort[] data = new ushort[] {
                    (ushort)(Mains[(int)PLCIndicatorEnum.TOP_SENSOR].Status ? 1 : 0),

                    (ushort)(Mains[(int)PLCIndicatorEnum.BOTTOM_SENSOR].Status ? 1 : 0),
                    0,
                    0,
                    (ushort)(Mains[(int)PLCIndicatorEnum.TOP_LOCK].Status ? 1 : 0),
                    (ushort)(Mains[(int)PLCIndicatorEnum.BOTTOM_LOCK].Status ? 1 : 0),
                    (ushort)(Mains[(int)PLCIndicatorEnum.GREEN_LAMP].Status ? 1 : 0),
                    (ushort)(Mains[(int)PLCIndicatorEnum.YELLOW_LAMP].Status ? 1 : 0),
                    (ushort)(Mains[(int)PLCIndicatorEnum.RED_LAMP].Status ? 1 : 0)
                };
                return Task.FromResult<ushort[]?>(data);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("ERR read plc: " + ex.Message);
                return Task.FromResult<ushort[]?>(null);
            }
        }
        public IDisposable Subscribe(IObserver<ushort[]> observer)
        {
            if (!Observers.Contains(observer) )
                Observers.Add(observer);
            return new Unsubscribe<ushort[]>(Observers, observer);
        }

        public async Task ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum indicator, bool state)
        {
            await SendCommand((ushort)indicator, state ? (ushort)1 : (ushort)0);
        }
        public void FinishObserver<T>()
        {
            var find = Observers.Where(x => x.GetType() == typeof(T)).ToArray();
            for (int i = 0; i < find.Length; i++)
                find[i].OnCompleted();
        }

        public IDisposable Subscribe(IObserver<bool> observer)
        {
            if (!LockUpdateObserver.Contains(observer))
                LockUpdateObserver.Add(observer);
            return new Unsubscribe<bool>(LockUpdateObserver, observer);
        }

        public void UpdateLockState(bool update)
        {
            for (int i = 0; i < LockUpdateObserver.Count; i++)
                LockUpdateObserver[i].OnNext(update);
        }
    }
}
