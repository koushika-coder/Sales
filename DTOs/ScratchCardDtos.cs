namespace Sales.DTOs
{
    public class ScratchCardAdminResponse
    {
        public int Id { get; set; }
        public string ScratchCardNo { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public int? ForcedOpenNo { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class SetOpenValueRequest
    {
        public int OpenValue { get; set; }
    }

    public class AddScratchCardRequest
    {
        public string ScratchCardNo { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
