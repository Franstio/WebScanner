namespace ScannerWeb.Models
{
    public class ConfigModel
    {
        public decimal MaxWeight { get; set; }
        public string ServerIP { get; set; } = string.Empty;
        public string SendWeightEndPoint { get; set; } = string.Empty;
        public string RealTimeWeightEndPoint { get; set; } = string.Empty;
        public string ReadStatusEndPoint { get; set; } = string.Empty;
        public string ApiPrefix { get; set; } = string.Empty;
        public string ArduinoCOM { get; set; } = string.Empty;
        public string PlcCOM { get; set; } = string.Empty;
    }
}
