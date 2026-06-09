namespace Sales.Models
{
    public class Paypoint
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public decimal PaypointValue { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    }
}
