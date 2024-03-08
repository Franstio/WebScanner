using System.Diagnostics;

namespace ScannerWeb.Observer
{
    public class ArduinoWeightObserver : IObserver<string>
    {
        private IDisposable? unsubscribe;
        public WeightReceived? WeightReceivedEvent { get; set; }
        public void OnCompleted()
        {
            Unsubscribe();
        }
        public void Subscribe(IObservable<string> Observable)
        {
            unsubscribe = Observable.Subscribe(this);
        }
        public void OnError(Exception error)
        {
            Trace.WriteLine(error.Message);
        }

        public async void OnNext(string message)
        {
            if (message.Contains("?") || string.IsNullOrEmpty(message) || message.Split(";").Length < 2)
            {
                Trace.WriteLine(message);
                return;
            }
            string[] data = message.Split(";");
            Trace.WriteLine($"msg:{message}\n0:{data[0]}\n1:{data[1]}");
            decimal _weight = 0;
            bool isValid = decimal.TryParse(data[0].Replace(" ", "").Trim(),out _weight);
            if (!isValid)
                return;
            if (WeightReceivedEvent is not null)
                await WeightReceivedEvent(_weight);
        }
        public delegate Task WeightReceived(decimal weight);
        public void Unsubscribe()
        {

            if (unsubscribe is not null)
                unsubscribe.Dispose();
        }
    }
}
