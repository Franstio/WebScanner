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
//            Trace.WriteLine(message);
            if (message.Contains("?") || string.IsNullOrEmpty(message) )
            {
                Trace.WriteLine(message);
                return;
            }
            string[] ar = message.Split('.');
            if (message.Contains(".") && ar.Length != 2)
                return;
            decimal _weight = 0;
            bool isValid = decimal.TryParse(message.Replace(" ", "").Trim(),out _weight);
//            Trace.WriteLine("isvalid: " + isValid);
            if (!isValid)
            {
                Trace.WriteLine(message);
                return;
            }
            if (_weight < -2 )
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
