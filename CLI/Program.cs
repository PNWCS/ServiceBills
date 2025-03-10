using System;
using Lib;

namespace CLI
{
    public class Program
    {
        public static void Main()
        {

            List<QBVendor> vendorList = QueryVendor.GetVendors();

            Console.WriteLine("Vendors in QuickBooks:");
            Console.WriteLine("---------------------------------------------------");
            Console.WriteLine("Name\t\tDate\t\tChartOfAccount\tAmount\tMemo");
            Console.WriteLine("---------------------------------------------------");

            foreach (QBVendor vendor in vendorList)
            {
                Console.WriteLine($"{vendor.Vendor}\t{vendor.Date}\t{vendor.ChartOfAccountName}\t{vendor.Amount}");
            }

            Console.WriteLine("---------------------------------------------------");
        }
    }
}
