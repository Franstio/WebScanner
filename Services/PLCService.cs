
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
                port.ReadTimeout = 200;
                port.WriteTimeout = 200;
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
            await SendCommand((ushort)indicator, state ? (ushort)0 : (ushort)1);
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
                        ushort[]? data = await ReadCommand(0, 10);
                        if (data is null)
                            continue;

                        logger.LogInformation(String.Join(",", data));
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

                        await Task.Delay(1000);
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
        public async Task SendCommand(ushort address, ushort value)
        {

            if (master is null)
            {
                logger.LogDebug("Master Modbus is null");
                return;
            }
            try
            {
                await master.WriteSingleRegisterAsync(SlaveId, address, value);
            }
            catch(Exception ex)
            {
                logger.LogDebug("Err Writing To PLc: " + ex.Message);
                await SendCommand(address, value);
            }
        }
        public async Task<ushort[]?> ReadCommand(ushort address, ushort numberOfPoint)
        {
            if (master is null)
            {
                logger.LogDebug("Master Modbus is null");
                return null;
            }
            try
            {
                return await master.ReadHoldingRegistersAsync(SlaveId, 0, 12);
            }
            catch(Exception ex)
            {
                logger.LogDebug("ERR read plc: "+ex.Message);
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
            return new Unsubscribe<bool>(LockUpdateObserver,observer);
        }

        public void UpdateLockState(bool update)
        {
            for (int i = 0; i < LockUpdateObserver.Count; i++)
                LockUpdateObserver[i].OnNext(update);
        }
    }
}
