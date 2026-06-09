namespace Sales.Models
{


    public class SafeDrop
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }
        public decimal LastSafe { get; set; }
        public decimal SafeDropAmount { get; set; }
        public decimal Cash => LastSafe + SafeDropAmount;  // calculated, not stored
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

}
