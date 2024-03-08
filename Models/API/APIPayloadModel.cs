namespace ScannerWeb.Models.API
{
    public class APIPayloadModel
    {
        public APIPayloadItemModel[] data { get; set; } = new APIPayloadItemModel[0];
        public class APIPayloadItemModel
        {
            public int? doorstatus { get; set; } = null;
            public string lastbadgeno { get; set; } = string.Empty;
            public string lastfrombinname { get; set; } = string.Empty;
        }
    }
}
