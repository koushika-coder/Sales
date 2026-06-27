using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sales.Models
{
    [Table("UserActiveDateOverrides")]
    public class UserActiveDateOverride
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        public DateOnly ActiveDate { get; set; }

        public DateTime SetAt { get; set; } = DateTime.UtcNow;
    }
}
