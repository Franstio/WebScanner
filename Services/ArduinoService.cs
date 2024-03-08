using Microsoft.Extensions.Options;
using ScannerWeb.Interfaces;
using ScannerWeb.Models;
using ScannerWeb.Observer;
using System.Diagnostics;
using System.IO.Ports;

namespace ScannerWeb.Services
{
    public class ArduinoService : IArduinoService
    {
        public string COM { get; set; } = "/dev/ttyUSB0";
        private List<IObserver<string>> Observers = new List<IObserver<string>>();
        public SerialPort? _sPort;
        public ArduinoService(IOptions<ConfigModel> opt)
        {
            _sPort = BuildSerialPort();
            COM = opt.Value.ArduinoCOM;
        }
        private SerialPort? BuildSerialPort()
        {
            try
            {
                if (COM == "")
                    return null;
                Trace.WriteLine("LOAD ARDUINO");
                SerialPort sPort = new SerialPort(COM);
                sPort.BaudRate = 4800;
                sPort.Parity = Parity.None;
                sPort.StopBits = StopBits.One;
                sPort.DataBits = 8;
                sPort.Handshake = Handshake.None;
                sPort.RtsEnable = true;
                sPort.DtrEnable = true;
                sPort.DataReceived += SPort_DataReceived;
                return sPort;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                return null;
            }
        }

        private  void SPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort? sPort = (SerialPort)sender;
                if (sPort is null)
                    return;
                string res = sPort.ReadLine();
                Trace.WriteLine(res);
                if (Observers is not null && Observers.Count > 0)
                {
                    for (int i=0;i<Observers.Count;i++)
                        Observers[i].OnNext(res);
                    //CleanObservers();
                }
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex.Message);
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
        public  Task Connect(CancellationToken token)
        {
            do
            {
                try
                {
                    if (_sPort is null)
                        return Task.CompletedTask;

                    if (_sPort.IsOpen)
                    {
                        Trace.WriteLine("Port Opened, Closing..");
                        return Task.CompletedTask;//_sPort.Close();
                    }
                    _sPort.Open();

                    Trace.WriteLine("OPEN ARDUINO");
                }
                catch(Exception ex)
                {
                    Trace.WriteLine(ex.Message);
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
