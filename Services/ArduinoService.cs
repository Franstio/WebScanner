using ApexCharts;
using Microsoft.Extensions.Options;
using ScannerWeb.Interfaces;
using ScannerWeb.Models;
using ScannerWeb.Observer;
using System.Diagnostics;
using System.IO.Ports;
using System.Reflection;
using System.Text;

namespace ScannerWeb.Services
{
    public class ArduinoService : IArduinoService
    {
        public string COM { get; set; } = "/dev/ttyUSB0";
        public string USB_ID { get; set; } = "";
        private List<IObserver<string>> Observers = new List<IObserver<string>>();
        public SerialPort? _sPort;
        private ILogger<ArduinoService> logger;
        private int counter = 0;
        private int totalFreeze = 0;
        private Task TaskRun = Task.CompletedTask;
        private CancellationToken listenerToken = CancellationToken.None;
        private CancellationTokenSource taskCancel = new CancellationTokenSource();
        private bool isRunning = false;
        public ArduinoService(IOptions<ConfigModel> opt,ILogger<ArduinoService> logger)
        {
            this.logger = logger;
            COM = opt.Value.ArduinoCOM;
            USB_ID = opt.Value.Arduino_USBID;
            _sPort = BuildSerialPort();
        }
        private SerialPort? BuildSerialPort()
        {
            try
            {
                if (COM == "")
                    return null;
                logger.LogDebug("LOAD ARDUINO");
                SerialPort sPort = new SerialPort(COM);
                sPort.BaudRate = 4800;
                sPort.Parity = Parity.None;
                sPort.StopBits = StopBits.One;
                sPort.DataBits = 8;
                sPort.Handshake = Handshake.None;
                sPort.RtsEnable = false;
                sPort.DtrEnable = false;
//                sPort.DataReceived += SPort_DataReceived;
//                sPort.ErrorReceived += SPort_ErrorReceived;
                sPort.ReadTimeout = 1200;

                return sPort;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                return null;
            }
        }

        private async void SPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            await ResetUSB();
            Func<SerialErrorReceivedEventArgs,Exception?,string > f = (SerialErrorReceivedEventArgs evet,Exception? exce) =>"Error Arduino Serial Reading";
            logger.Log(LogLevel.Error, new EventId(), e, null, f);
            try
            {
                SerialPort _sp = (SerialPort)sender;
                logger.LogError("Error Reading Serial Arduino: " +_sp.ReadLine());
            }
            catch(Exception ex)
            {
                logger.LogError(ex.Message);
            }
        }

        public  Task StartListening(CancellationToken token)
        {
            if (COM == string.Empty)
                return Task.CompletedTask;
            return Task.CompletedTask;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_sPort is null)
                    {
                        logger.LogDebug("LISTENER PORT IS NULL");
                        continue;
                    }
                    string res = _sPort.ReadLine();
                    logger.LogInformation("ARDUINO OUTPUT: "+res);
                    if (Observers is not null && Observers.Count > 0)
                    {
                        for (int i = 0; i < Observers.Count; i++)
                            Observers[i].OnNext(res);
                        //CleanObservers();
                    }
                }
                catch(Exception ex)
                {
                    logger.LogInformation("ARDUINO LISTENER: "+ex.Message);
                }
            }
            return Task.CompletedTask;
        }
        private async void SPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            isRunning = true;
            await ReadData((SerialPort)sender);
        }
        private async Task ReadData(SerialPort portData)
        {
            try
            {
                SerialPort? sPort = portData;
                if (sPort is null)
                    return;
//                byte[] buffer = new byte[200];
  //              sPort.Read(buffer, 0, buffer.Length);
                string res = sPort.ReadExisting();
                var ar = res.Split('\n');
                decimal _o = 0;
                logger.LogCritical("DATA RAW1:" + res);
                List<decimal> _data = new List<decimal>();
                foreach (var a in ar)
                {
                    if (!decimal.TryParse(a, out _o))
                    {
                        await _sPort!.BaseStream.FlushAsync();
                        continue;
                    }
                    logger.LogCritical("DATA: " + _o);
                    _data.Add(_o);
                }
                logger.LogCritical("DATA MIN: " + _data.Min());
                logger.LogCritical("Observer Count: " + Observers?.Count);
                if (Observers is not null && Observers.Count > 0 && _data.Count > 0)
                {
                    for (int i = 0; i < Observers.Count; i++)
                        Observers[i].OnNext(_data.Min().ToString("0.00"));
                    //CleanObservers();
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex.Message + " | " + ex.StackTrace);
                await Disconnect();
            }
        }
        private void CleanObservers()
        {
            var obs = new List<IObserver<string>>();
            for (int i = 0; i < Observers.Count; i++)
                if (Observers[i] is not null)
                    obs.Add(Observers[i]);
            Observers = obs;
        }
        public   Task Connect(CancellationToken token)
        {
            do
            {
                try
                {
                    if (_sPort is null)
                        continue;

                    if (_sPort.IsOpen)
                    {
                        logger.LogDebug("Port Opened, Closing..");
                        return Task.CompletedTask;//_sPort.Close();
                    }
                    _sPort.Open();
                    byte[] buffer = Encoding.UTF8.GetBytes("\n");
                    _sPort.Write(buffer,0,buffer.Length);
                    logger.LogDebug("OPEN ARDUINO");
                    TaskRun = Task.Run(async delegate
                    {
                        while (!taskCancel.IsCancellationRequested)
                        {
                            if (_sPort is null)
                            {
                                logger.LogInformation("Port object is nul, retrying...");
                                continue;
                            }
                            if (!_sPort.IsOpen)
                            {

                                _sPort = BuildSerialPort() ;
                                if (_sPort is null)
                                {
                                    logger.LogInformation("Port object is nul, retrying...");
                                    continue;
                                }
                                _sPort.Open();
                                byte[] buffer = Encoding.UTF8.GetBytes("READ\n");
                                _sPort.Write(buffer, 0, buffer.Length);
                                buffer = Encoding.UTF8.GetBytes("CMD,1234\n");
                                _sPort.Write(buffer, 0, buffer.Length);
                                logger.LogCritical("OPEN ARDUINO");
                            }
                            await ReadData(_sPort);
                            await Task.Delay(2000);
                        }
                        taskCancel = new CancellationTokenSource();
                        await Connect(listenerToken);
                    });
                }
                catch(Exception ex)
                {
                    Disconnect().RunSynchronously();
                    _sPort = BuildSerialPort();
                    logger.LogError(ex.Message + " | " + ex.StackTrace);
                }
            }            
            while (_sPort is not null && !token.IsCancellationRequested && !_sPort.IsOpen) ;
            return Task.CompletedTask;
        }
        private async Task ResetUSB()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo() { FileName = "/bin/bash", Arguments = $"-c \"sudo usbreset {USB_ID}", RedirectStandardOutput = true };
                Process proc = new Process() { StartInfo = startInfo, };
                proc.Start();
                string result = await proc.StandardOutput.ReadToEndAsync();
                logger.LogCritical($"Reset USB Arduino: {result}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error Reset USB: {ex.Message} {ex.InnerException?.Message}\n{ex.StackTrace}");
            }
        }
        public async Task Disconnect()
        {
            if (_sPort is null)
                return;
            if (Observers is not null && Observers.Count > 0)
            {
                for (int i=0;i<Observers.Count;i++)
                    Observers[i].OnCompleted();
            }
            _sPort.Close();
            logger.LogInformation($"Connection Status: {_sPort.IsOpen}");
            await Task.Delay(1000);
            await ResetUSB();
            await Task.Delay(1000);
            
        }

        public async void Dispose()
        {
            await Disconnect();
        }


        public Task SendCommand(string cmd)
        {
            if (_sPort is null)
                return Task.CompletedTask;
            _sPort.WriteLine(cmd);
            return Task.CompletedTask;
        }

        public IDisposable Subscribe(IObserver<string> observer)
        {
            if (!Observers.Contains(observer))
                Observers.Add(observer);
            return new Unsubscribe<string>(Observers, observer);
        }
        

    }
}
