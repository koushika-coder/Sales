using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sales.Models
{
    [Table("SupplierInvoices")]
    public class SupplierInvoice
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        public int SupplierId { get; set; }

        [Required]
        [MaxLength(100)]
        public string InvoiceNo { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Value { get; set; }

        public DateTime CreatedAt { get; set; }

        // Navigation Property
        [ForeignKey(nameof(SupplierId))]
        public Supplier Supplier { get; set; } = null!;
    }
}