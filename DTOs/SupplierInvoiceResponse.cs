namespace Sales.DTOs
{
    public class SupplierInvoiceResponse
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? UserName { get; set; }
    }

    public class InvoiceDateSummary
    {
        public DateOnly Date { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalValue { get; set; }
    }
}
