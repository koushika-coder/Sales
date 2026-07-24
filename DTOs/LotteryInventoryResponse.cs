namespace Sales.DTOs
{
    public class LotteryInventoryResponse
    {
        public int Id { get; set; }         // 0 = no record saved yet for active date

        public int LotteryId { get; set; }

        public string ScratchCardNo { get; set; }

        public decimal Price { get; set; }

        public int OpenNo { get; set; }

        public int CloseNo { get; set; }

        // null = no record saved yet for the active date (Id == 0); lets the frontend
        // tell "not entered" apart from a genuinely saved zero.
        public int? TotalSold { get; set; }

        public decimal? Sales { get; set; }
    }
}
