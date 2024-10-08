﻿using Microsoft.Extensions.Options;
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
        private List<IObserver<string>> Observers = new List<IObserver<string>>();
        public SerialPort? _sPort;
        private ILogger<ArduinoService> logger;
        private int counter = 0;
        private int totalFreeze = 0;
        private Task TaskRun = Task.CompletedTask;
        private CancellationToken listenerToken = CancellationToken.None;
        private CancellationTokenSource taskCancel = new CancellationTokenSource();
        public ArduinoService(IOptions<ConfigModel> opt,ILogger<ArduinoService> logger)
        {
            this.logger = logger;
            _sPort = BuildSerialPort();
            COM = opt.Value.ArduinoCOM;
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
                sPort.RtsEnable = true;
//                sPort.DtrEnable = true;
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

        private void SPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
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
                logger.LogInformation(ex.Message);
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
                    byte[] buffer = Encoding.ASCII.GetBytes("\n");
                    _sPort.Write(buffer,0,buffer.Length);
                    logger.LogDebug("OPEN ARDUINO");
                    TaskRun = Task.Run(async delegate
                    {
                        while (!taskCancel.IsCancellationRequested)
                        {
                            await ReadData(_sPort);
                            await Task.Delay(2000);
                        }
                        taskCancel = new CancellationTokenSource();
                        await Connect(listenerToken);
                    });
                }
                catch(Exception ex)
                {
                    logger.LogError(ex.Message);
                }
            }            
            while (_sPort is not null && !token.IsCancellationRequested && !_sPort.IsOpen) ;
            return Task.CompletedTask;
        }

        public Task Disconnect()
        {
            if (_sPort is null)
                return Task.CompletedTask;
            if (Observers is not null && Observers.Count > 0)
            {
                for (int i=0;i<Observers.Count;i++)
                    Observers[i].OnCompleted();
            }
            _sPort.Close();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Disconnect();
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
