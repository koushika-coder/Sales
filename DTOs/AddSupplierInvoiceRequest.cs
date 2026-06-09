namespace Sales.DTOs
{
    public class AddSupplierInvoiceRequest
    {
        public int SupplierId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }
}
