// Models/Deduction.cs
namespace Sales.Models
{
    public class Deduction
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public decimal Cashback { get; set; }
        public decimal PaypointPayout { get; set; }
        public decimal InstantLotteryPayout { get; set; }
        public decimal NewsVoucher { get; set; }
        public decimal DDPoint { get; set; }
        public decimal LotteryPayout { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}