namespace Sales.Models
{
    namespace Sales.Models
    {
        public class Lottery
        {
            public int Id { get; set; }

            public int UserId { get; set; }

            public decimal LotteryValue { get; set; }

            public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

            public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
        }
    }
}
