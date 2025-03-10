using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceBillsLib
{
    public class QBVendor
    {

            public string Vendor { get; set; }
            public string Date { get; set; }
            public string ChartOfAccountName { get; set; }
            public string Amount { get; set; }

            public QBVendor(string vendor, string date, string chartOfAccountName, string amount)
            {
                Vendor = vendor;
                Date = date;
                ChartOfAccountName = chartOfAccountName;
                Amount = amount;
            }

           

        }
    }

