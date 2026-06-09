namespace Sales.DTOs
{
    public class LotteryInventoryResponse
    {
        public int LotteryId { get; set; }

        public string ScratchCardNo { get; set; }

        public decimal Price { get; set; }

        public int OpenNo { get; set; }

        public int CloseNo { get; set; }

        public int TotalSold { get; set; }

        public decimal Sales { get; set; }
    }
}
