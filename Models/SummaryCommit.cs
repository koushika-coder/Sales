namespace Sales.Models
{
    public class SummaryCommit
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateOnly Date { get; set; }
        public decimal SummaryTotal { get; set; }
        public decimal ZReportTotal { get; set; }
        public decimal Difference { get; set; }
        public DateTime CommittedAt { get; set; }

        public User? User { get; set; }
    }
}
