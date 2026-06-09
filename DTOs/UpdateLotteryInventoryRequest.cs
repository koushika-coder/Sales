namespace Sales.DTOs
{
    public class UpdateLotteryInventoryRequest
    {
        public int Id { get; set; }

        public decimal Price { get; set; }

        public int OpenNo { get; set; }

        public int CloseNo { get; set; }
    }
}
