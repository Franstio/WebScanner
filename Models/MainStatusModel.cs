using static ScannerWeb.Observer.PLCIndicatorObserver;

namespace ScannerWeb.Models
{
    public class MainStatusModel
    {
        public string StatusDesc { set; get; } = string.Empty;
        public bool Status { get; set; } = false;
        public MainStatusModel(string statusDesc)
        {
            StatusDesc = statusDesc;
        }
        public MainStatusModel(string statusDesc,bool status)
        {
            StatusDesc = statusDesc;
            Status = status;
        }
        public static List<MainStatusModel> BuildStatusModel(string[]? status=null)
        {
            List<MainStatusModel> data =new List<MainStatusModel>();
            if (status is null)
            {
                data.AddRange(new List<MainStatusModel>()
                {
                    new MainStatusModel("Top Sensor"),
                    new MainStatusModel("Bottom Sensor"),
                    new MainStatusModel("Top Door"),
                    new MainStatusModel("Bottom Door"),
                    new MainStatusModel("Status Server")
                });
            }
            else
            {
                for (int i = 0;i<status.Length;i++)
                {
                    data.Add(new MainStatusModel(status[i]));
                }
            }
            return data;
        }

        public static MainStatusModel CreateStatusModel(PLCIndicatorEnum statusName, bool status)
        {
            return new MainStatusModel(Enum.GetName(statusName)!.Replace("_", " "), status);
        }
    }
}
