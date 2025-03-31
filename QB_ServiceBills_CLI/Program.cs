using QB_ServiceBills_Lib;

namespace QB_ServiceBills_CLI
{
    public class Program
    {
        public static void Main()k
        public static void Main()k
        {
            List<ServiceBill> serviceBills = ServiceBillReader.QueryAllServiceBills();

            Console.WriteLine("Vendors in QuickBooks:");
            Console.WriteLine($"{"Vendor",-20}{"\tDate",-15}{"\t\t\tAccountName",-30}{"\t\tAmount",10}");


            foreach (ServiceBill bill in serviceBills)
            {
                foreach (var line in bill.Lines)
                {
                    Console.WriteLine($"{bill.VendorName,-20}\t{bill.BillDate:yyyy-MM-dd}\t{bill.Memo}\t{bill.InvoiceNum}\t{line.AccountName,-30}\t\t${line.Amount,8:F2}");
                }
            }
        }
    }
}




