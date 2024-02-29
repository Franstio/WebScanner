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
    }
}
