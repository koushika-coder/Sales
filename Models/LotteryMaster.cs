namespace Sales.Models
{
    public class LotteryMaster
    {
        public int Id { get; set; }

        public string ScratchCardNo { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
