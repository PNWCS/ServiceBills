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
    }

    public class ServiceBillLine
    {
        public string AccountName { get; set; } = "";
        public double Amount { get; set; }
    }
}
