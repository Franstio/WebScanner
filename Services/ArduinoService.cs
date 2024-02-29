using System.Diagnostics;
using System.IO.Ports;

namespace ScannerWeb.Services
{
    public class ArduinoService : ISerialService
    {
        public string COM = "";
        public SerialPort _sPort ;
        public Action<string>? act { get; set; }
        public ArduinoService()
        {
            _sPort = BuildSerialPort();
        }
        private SerialPort BuildSerialPort()
        {
	    try
	    {
		if (COM == "")
			return null;
            SerialPort sPort = new SerialPort(COM);
            sPort.BaudRate = 9600;
            sPort.Parity = Parity.None;
            sPort.StopBits = StopBits.One;
            sPort.DataBits = 8;
            sPort.Handshake = Handshake.None;
            sPort.RtsEnable = true;
            sPort.DataReceived += SPort_DataReceived;
            return sPort;
	    }
	    catch (Exception ex)
	    {
		Console.WriteLine(ex.Message);
		return null;
	    }
        }

        private async void SPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sPort = (SerialPort)sender;
            string res = sPort.ReadLine();
            if (act is not null)
                act(res);
            await SendCommand("M");
        }

        public async Task Connect()
        {
	    if (_sPort is null)
		return;
            _sPort.Open();
            await SendCommand("M");
        }

        public Task Disconnect()
        {
            _sPort.Close();
            _sPort = BuildSerialPort();
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

    }
}
