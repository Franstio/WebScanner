
using System.Diagnostics.Metrics;
using System.Diagnostics;
using NModbus.Serial;
using NModbus;
using System.IO.Ports;
using System;
using ScannerWeb.Observer;
using Microsoft.Extensions.Options;
using ScannerWeb.Models;
using ScannerWeb.Interfaces;

namespace ScannerWeb.Services
{
    public class PLCService : IPLCService
    {
        private List<IObserver<ushort[]>> Observers = new List<IObserver<ushort[]>>();
        private List<IObserver<bool>> LockUpdateObserver = new List<IObserver<bool>>();
        ModbusFactory factory = new ModbusFactory();
        SerialPort? _port;
        IModbusMaster? master;
        public string COM { get; set; } = "/dev/ttyUSB1";
        public byte SlaveId { get; set; } = 1;
        private bool isRunning = false;
        private ILogger<PLCService> logger;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private List<Tuple<ushort, ushort>> PayloadCommand = [];
        public PLCService(IOptions<ConfigModel> opt,ILogger<PLCService> _logger)
        {
            logger = _logger;
            master = BuildModbusMaster();
            COM = opt.Value.PlcCOM;

        }
        private SerialPort? BuildSerialPort()
        {
            try
            {

                if (COM == string.Empty)
                    return null;
                logger.LogDebug("LOAD PLC");
                SerialPort port = new SerialPort(COM);
                port.BaudRate = 9600;
                port.DataBits = 8;
                port.Parity = Parity.None;
                port.StopBits = StopBits.One;
                port.ReadTimeout = 1000;
                port.WriteTimeout = 1000;
                return port;
            }
            catch(Exception ex)
            {
                logger.LogError(ex.Message);
                return null;
            }
        }
        private IModbusMaster? BuildModbusMaster()
        {
            try
            {
                _port = BuildSerialPort();
                return _port is null ? null : factory.CreateRtuMaster(_port);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                //MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                //Process.GetCurrentProcess().Kill();
                return null;
            }
        }
        public Task Connect(CancellationToken ctoken)
        {
            do
            {
                try
                {
                    cts = new CancellationTokenSource();
                    if (_port is null)
                        logger.LogDebug("Port is null");
                    else
                    {

                        logger.LogDebug("OPEN PLC");
                        _port.Open();
                        
                    }
                }
                catch(Exception ex)
                { 
                    logger.LogError(ex.Message);
                }
            }
            while (_port is not null && !ctoken.IsCancellationRequested && !_port.IsOpen) ;
            return Task.CompletedTask;
        }
        public async Task TriggerManual(PLCIndicatorObserver.PLCIndicatorEnum indicator, bool state)
        {
            logger.LogCritical($"Indicator: {indicator}, state: {(state ? 1 : 0)} ");
                await SendCommand((ushort)indicator, state ? (ushort)0 : (ushort)1,isRunning);
        }
        public async Task StartReadingInput(CancellationToken token, ushort count)
        {
            if (COM == string.Empty)
                return;
            logger.LogDebug("Start Reading PLC");
            try
            {
                isRunning = true;
                while (!token.IsCancellationRequested && !cts.IsCancellationRequested)
                {
                    try
                    {
                        await RunCommand();
                        ushort[]? data = await ReadCommand(0, 10);
                        if (data is null)
                            continue;
                        for (int i = 0; i < Observers.Count; i++)
                            if (Observers[i] != null)
                                Observers[i].OnNext(data);
                        //CleanObservers();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.Message + "...Reconnecting.");
                        if (_port is not null)
                        {
                            _port.Close();
                            _port.Dispose();
                        }
                        master = BuildModbusMaster();
                        _port!.Open();
                    }
                    finally
                    {

                        await Task.Delay(10);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Connect Error:" +ex.Message);
            }
            finally
            {
                isRunning = false;
            }
        }
        private void CleanObservers()
        {
            var obs = new List<IObserver<ushort[]>>();
            for (int i = 0; i < Observers.Count; i++)
                if (Observers[i] is not null)
                    obs.Add(Observers[i]);
            Observers = obs;
        }
        public Task Disconnect()
        {

            if (_port is null)
                return Task.CompletedTask;
            if (Observers is not null && Observers.Count > 0)
            {
                for (int i = 0; i < Observers.Count; i++)
                    Observers[i].OnCompleted();
            }
            _port.Close();
            isRunning = false;
            cts.Cancel();
            cts.Dispose();
            cts = new CancellationTokenSource();
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
        public async Task SendCommand(ushort address, ushort value,bool suspend=false)
        {
            if (suspend)
            {
                PayloadCommand.Add(new Tuple<ushort, ushort>(address, value));
                return;
            }
            if (master is null)
            {
                logger.LogError("Master Modbus is null");
                return;
            }
            try
            {
                await master.WriteSingleRegisterAsync(SlaveId, address, value);
            }
            catch(Exception ex)
            {
                if (ex.Message.Contains("Unexpected"))
                    return ;
                logger.LogDebug("Err Writing To PLc: " + ex.Message);
                logger.LogError("ERR read plc: " + ex.Message);
                await Reconnect();
                await SendCommand(address, value,suspend);
            }
        }
        public  async Task Reconnect()
        {
            try
            {
                logger.LogError("...Reconnecting.");
                if (_port is not null)
                {
                    _port.Close();
                    _port.Dispose();
                }
                master = BuildModbusMaster();
                _port!.Open();
            }
            catch (Exception ex)
            {
                logger.LogError("Fail Reconnecting, "+ex.Message);
            }
            await Task.Delay(1000);
        }
        public async Task<ushort[]?> ReadCommand(ushort address, ushort numberOfPoint)
        {
            ushort[]? data = null;
            if (master is null)
            {
                logger.LogDebug("Master Modbus is null");
                return null;
            }
            try
            {
                data  = await master.ReadHoldingRegistersAsync(SlaveId, address, numberOfPoint);
                return data;
            }
            catch(Exception ex)
            {
                if (ex.Message.Contains("Unexpected"))
                    return data;
                logger.LogError("ERR read plc: " + ex.Message);
                logger.LogError( "...Reconnecting.");
                await Reconnect();
                return await ReadCommand(address,numberOfPoint);
            }
        }
        public IDisposable Subscribe(IObserver<ushort[]> observer)
        {
            if (!Observers.Contains(observer))
                Observers.Add(observer);
            return new Unsubscribe<ushort[]>(Observers,observer);
        }

        public async Task ToggleManual(PLCIndicatorObserver.PLCIndicatorEnum indicator, bool state)
        {
            await SendCommand((ushort)indicator, state ? (ushort)1 : (ushort)0,isRunning);
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
            return new Unsubscribe<bool>(LockUpdateObserver,observer);
        }

        public void UpdateLockState(bool update)
        {
            for (int i = 0; i < LockUpdateObserver.Count; i++)
                LockUpdateObserver[i].OnNext(update);
        }

        public async Task RunCommand()
        {
            Tuple<ushort, ushort>[] payload = new Tuple<ushort,ushort>[PayloadCommand.Count];
            PayloadCommand.CopyTo(payload,0);
            PayloadCommand.Clear();
            if (payload.Length < 1 || payload[0] == null)
            {
                return;
            }
            for (int i=0;i<payload.Length;i++)
            {
                logger.LogCritical($"Send Command with address {payload[i].Item1} and value {payload[i].Item2}");
                await SendCommand(payload[i].Item1, payload[i].Item2, false);
            }

        }
    }
}
