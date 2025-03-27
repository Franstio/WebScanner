using ScannerWeb.Interfaces;
using ScannerWeb.Observer;
using System.Diagnostics;

namespace ScannerWeb.Mock
{
    public class ArduinoMockService : IArduinoService
    {
        public string COM { get; set; } = string.Empty;
        private List<IObserver<string>> Observers = new List<IObserver<string>>();
        private CancellationToken cToken;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private bool isRunning = false;
        private ILogger<ArduinoMockService> logger;
        public ArduinoMockService(ILogger<ArduinoMockService> logger)
        {
            this.logger = logger;
        }
        public Task Connect(CancellationTokenSource token)
        {
            cToken = token.Token;
            Task.Run(doWork);
            return Task.CompletedTask;
        }
        private void CleanObservers()
        {
            var obs = new List<IObserver<string>>();
            for (int i = 0; i < Observers.Count; i++)
                if (Observers[i] is not null)
                    obs.Add(Observers[i]);
            Observers = obs;
        }
        private async Task doWork()
        {
            if (isRunning)
                return;
            try
            {
                Random rnd = new Random();
                isRunning = true;
                while (!cToken.IsCancellationRequested && !cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        decimal weight = rnd.Next(0, 100);
                        string payload = $"{weight};0;0;0";
                        logger.LogInformation(payload);
                        for (int i = 0; i < Observers.Count; i++)
                            if (Observers[i] is not null)
                            Observers[i].OnNext(payload);
                        //CleanObservers();
                        await Task.Delay(500);
                    }
                    catch(Exception ex) {
                        logger.LogError(ex.Message);
                    }
                }
                isRunning = false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
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
            cts = new CancellationTokenSource();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Disconnect();
        }

        public Task SendCommand(string cmd)
        {
            return Task.CompletedTask;
        }

        public IDisposable Subscribe(IObserver<string> observer)
        {
            if (!Observers.Contains(observer)) 
                Observers.Add(observer);
            return new Unsubscribe<string>(Observers,observer);
        }

        public async Task StartListening(CancellationTokenSource token)
        {
            await doWork();
        }

        public Task CloseConnection()
        {
            throw new NotImplementedException();
        }

        public string GetConnectionStatus()
        {
            throw new NotImplementedException();
        }
    }
}
