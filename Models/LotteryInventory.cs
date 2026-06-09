namespace Sales.Models
{
    public class LotteryInventory
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int LotteryId { get; set; }

        public DateTime InventoryDate { get; set; }

        public int OpenNo { get; set; }

        public int CloseNo { get; set; }

        public int TotalSold { get; set; }

        public decimal Sales { get; set; }

        public int CreatedByUserId { get; set; }

        public DateTime CreatedDate { get; set; }

        public int? UpdatedByUserId { get; set; }

        public DateTime? UpdatedDate { get; set; }

        public User User { get; set; }

        public LotteryMaster LotteryMaster { get; set; }
    }
}