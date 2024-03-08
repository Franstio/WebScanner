namespace ScannerWeb.Models.API
{
    public class MainProcessModel
    {
        public int Step { get; set; } = -1;
        public APIPayloadModel.APIPayloadItemModel Payload { get; set; } = new APIPayloadModel.APIPayloadItemModel();
        public ProcessType Type { get; set; }
        public enum ProcessType
        {
            Top,
            Bottom
        }
        public string Instruction { get; set; } = string.Empty;
        public bool FinalStep { get; set; } = false;
    }
}
