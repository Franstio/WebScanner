using ScannerWeb.Models;
using System.Diagnostics;

namespace ScannerWeb.Observer
{
    public class PLCIndicatorObserver : IObserver<ushort[]>
    {
        private IDisposable? unsubscribe;
        public IndicatorUpdate? IndicatorUpdateEvent { get; set; }
        public void OnCompleted()
        {
            Unsubscribe();
        }

        public void OnError(Exception error)
        {
            Trace.WriteLine(error.Message);
        }
        public void Unsubscribe()
        {
            if (unsubscribe is not null)
                unsubscribe.Dispose();
        }
        public void Subscribe(IObservable<ushort[]> observable)
        {
            unsubscribe = observable.Subscribe(this);
        }

        public async void OnNext(ushort[] value)
        {
            Trace.WriteLine("Input PLC: "+string.Join(",", value));
            MainStatusModel[] Status = new MainStatusModel[7];
            if (value.Length < 7)
            {
                Trace.WriteLine("PLC Return value is less than 6 items of array");
            }
            Status[0] = MainStatusModel.CreateStatusModel(PLCIndicatorEnum.TOP_SENSOR, value[0] != 0);
            Status[1] = MainStatusModel.CreateStatusModel(PLCIndicatorEnum.BOTTOM_SENSOR, value[1] != 0);
            Status[2] = MainStatusModel.CreateStatusModel(PLCIndicatorEnum.TOP_LOCK, value[4] != 0);
            Status[3] = MainStatusModel.CreateStatusModel(PLCIndicatorEnum.BOTTOM_LOCK, value[5] != 0);
            Status[4] = MainStatusModel.CreateStatusModel(PLCIndicatorEnum.GREEN_LAMP, value[6] != 0);
            Status[5] = MainStatusModel.CreateStatusModel(PLCIndicatorEnum.YELLOW_LAMP, value[7] != 0);
            Status[6] = MainStatusModel.CreateStatusModel(PLCIndicatorEnum.RED_LAMP, value[8] != 0);
            if (IndicatorUpdateEvent is not null)
                await IndicatorUpdateEvent(Status);
        }
        public delegate Task IndicatorUpdate(MainStatusModel[] statuses);
        public enum PLCIndicatorEnum
        {
            TOP_SENSOR=0,
            BOTTOM_SENSOR=1,
            TOP_LOCK=4,
            BOTTOM_LOCK=5,
            GREEN_LAMP=6,
            YELLOW_LAMP=7,
            RED_LAMP=8
        }
    }
}
