using QB_ServiceBills_Lib;

namespace QB_ServiceBills_CLI
{
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine("===== Current Service Bills in QuickBooks =====");
            DisplayServiceBills(ServiceBillReader.QueryAllServiceBills());

            var newBills = new List<ServiceBill>
            {

                new ServiceBill
                {
                    VendorName = "A",
                    BillDate = DateTime.Today,
                    Memo = "Demo202",
                    InvoiceNum = "SBILL_DEMO302",
                    Lines = new List<ServiceBillLine>
                    {
                        new ServiceBillLine("Computer and Internet Expenses", 45.00),
                        new ServiceBillLine("Office Supplies", 10.00)
                    }
                },
                new ServiceBill
                {
                    VendorName = "B",
                    BillDate = DateTime.Today,
                    Memo = "Demo203",
                    InvoiceNum = "SBILL_DEMO303",
                    Lines = new List<ServiceBillLine>
                    {
                        new ServiceBillLine("Telephone Expense", 30.00),
                        new ServiceBillLine("Automobile Expense", 20.00)
                    }
                }
            };

            ServiceBillAdder.AddServiceBills(newBills);

            Console.WriteLine("\n===== Updated Service Bills in QuickBooks =====");
            DisplayServiceBills(ServiceBillReader.QueryAllServiceBills());
        }

        private static void DisplayServiceBills(List<ServiceBill> serviceBills)
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine($"{"Vendor",-20}{"Date",-15}{"AccountName",-35}{"Amount",10}");
            Console.WriteLine("--------------------------------------------------------------------------------");

            foreach (var bill in serviceBills)
            {
                foreach (var line in bill.Lines)
                {
                    Console.WriteLine($"{bill.VendorName,-20}{bill.BillDate.ToString("yyyy-MM-dd"),-15}{line.AccountName,-35}${line.Amount,10:F2}");
                }
            }
        }
    }
}
