namespace Sales.DTOs
{
    public class SafeDropRequest
    {
        public decimal LastSafe { get; set; }
        public decimal SafeDropAmount { get; set; }
    }
    public class SafeDropResponse
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }
        public decimal LastSafe { get; set; }
        public decimal SafeDropAmount { get; set; }
        public decimal Cash { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
