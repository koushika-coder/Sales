namespace Sales.DTOs
{
    public class LotteryInventoryReportResponse
    {
        public int Id { get; set; }

        public string ScratchCardNo { get; set; }

        public decimal Price { get; set; }

        public int OpenNo { get; set; }

        public int CloseNo { get; set; }

        public int TotalSold { get; set; }

        public decimal Sales { get; set; }

        public DateTime InventoryDate { get; set; }

        public bool IsCommitted { get; set; }
    }
}
