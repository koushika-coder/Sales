namespace Sales.DTOs
{
    public class LotteryInventorySaveRequest
    {
        public int LotteryId { get; set; }

        public int OpenNo { get; set; }

        public int CloseNo { get; set; }

        public DateTime InventoryDate { get; set; }

        public  DateTime UpdatedDate { get; set; }
        public int? UpdatedByUserId { get; set; }


    }
}
