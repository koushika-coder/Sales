namespace Sales.Models
{
    public class CreditCardBanking
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public decimal ManualCardAmount { get; set; }

        public decimal CardAmount { get; set; }

        public DateTime CreatedDate { get; set; }

        public User? User { get; set; }
    }
}
