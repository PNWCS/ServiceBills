namespace QB_ServiceBills_Lib
{
    public class ServiceBill
    {
        public string QBID { get; set; } = "";
        public string VendorName { get; set; } = "";
        public DateTime BillDate { get; set; }
        public string Memo { get; set; } = "";
        public string InvoiceNum { get; set; } = "";
        public List<ServiceBillLine> Lines { get; set; } = new();
        public ServiceBillStatus Status { get; set; } = ServiceBillStatus.Unknown;  // <-- ADD THIS PROPERTY

        public ServiceBill() { }

        public ServiceBill(string QBID, string VendorName, DateTime BillDate, string Memo, string InvoiceNum, List<ServiceBillLine> Lines)
        {
            this.QBID = QBID;
            this.VendorName = VendorName;
            this.BillDate = BillDate;
            this.Memo = Memo;
            this.InvoiceNum = InvoiceNum;
            this.Lines = Lines;
            this.Status = ServiceBillStatus.Unknown;
        }
    }

    public class ServiceBillLine
    {
        public string AccountName { get; set; } = "";
        public double Amount { get; set; }

        public ServiceBillLine() { }

        public ServiceBillLine(string AccountName, double Amount)
        {
            this.AccountName = AccountName;
            this.Amount = Amount;
        }
    }

    public enum ServiceBillStatus
    {
        Unknown,
        Unchanged,
        Different,
        Added,
        FailedToAdd,
        Missing
    }
}