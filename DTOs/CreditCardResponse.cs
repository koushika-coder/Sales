namespace Sales.DTOs
{
    public class CreditCardResponse
    {
        public int Id { get; set; }
        public decimal ManualCardAmount { get; set; }
        public decimal CardAmount { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CreditCardRequest
    {
     
        public decimal ManualCardAmount { get; set; }

        public decimal CardAmount { get; set; }
    }
}
