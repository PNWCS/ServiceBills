using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace QB_ServiceBills_Lib
{
    public static class ServiceBillComparator
    {
        private static List<ServiceBill> _simulatedAddedBills = new List<ServiceBill>();

        public static List<ServiceBill> CompareServiceBill(List<ServiceBill> inputBills)
        {
            Log.Information("ServiceBillComparator Initialized");

            var qbBills = ServiceBillReader.QueryAllServiceBills();

            // ✅ Add simulated in-memory bills to QB list for testing purposes
            qbBills.AddRange(_simulatedAddedBills);

            var results = new List<ServiceBill>();

            var qbLookup = qbBills.ToDictionary(
                b => $"{b.VendorName}|{b.InvoiceNum}",
                b => b,
                StringComparer.OrdinalIgnoreCase);

            var inputLookup = inputBills.ToDictionary(
                b => $"{b.VendorName}|{b.InvoiceNum}",
                b => b,
                StringComparer.OrdinalIgnoreCase);

            foreach (var input in inputBills)
            {
                string key = $"{input.VendorName}|{input.InvoiceNum}";

                if (!qbLookup.ContainsKey(key))
                {
                    input.Status = ServiceBillStatus.Added;
                    input.QBID = GenerateSimulatedQBID();

                    // ✅ Track newly added bills for future “Missing” detection
                    _simulatedAddedBills.Add(CloneBill(input));
                }
                else
                {
                    var qbBill = qbLookup[key];
                    input.QBID = qbBill.QBID;

                    if (IsSameBill(qbBill, input))
                    {
                        input.Status = ServiceBillStatus.Unchanged;
                    }
                    else
                    {
                        input.Status = ServiceBillStatus.Different;
                    }
                }

                results.Add(input);
                Log.Information($"ServiceBill {input.InvoiceNum} is {input.Status}.");
            }

            foreach (var qb in qbBills)
            {
                string key = $"{qb.VendorName}|{qb.InvoiceNum}";

                if (!inputLookup.ContainsKey(key))
                {
                    qb.Status = ServiceBillStatus.Missing;
                    results.Add(qb);
                    Log.Information($"ServiceBill {qb.InvoiceNum} is {qb.Status}.");
                }
            }

            Log.Information("ServiceBillComparator Completed");
            return results;
        }

        private static bool IsSameBill(ServiceBill qbBill, ServiceBill inputBill)
        {
            if (!string.Equals(qbBill.Memo, inputBill.Memo, StringComparison.OrdinalIgnoreCase))
                return false;

            if (qbBill.Lines.Count != inputBill.Lines.Count)
                return false;

            foreach (var qbLine in qbBill.Lines)
            {
                if (!inputBill.Lines.Any(l =>
                        string.Equals(l.AccountName, qbLine.AccountName, StringComparison.OrdinalIgnoreCase) &&
                        Math.Abs(l.Amount - qbLine.Amount) < 0.01))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GenerateSimulatedQBID()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static ServiceBill CloneBill(ServiceBill original)
        {
            return new ServiceBill
            {
                QBID = original.QBID,
                VendorName = original.VendorName,
                BillDate = original.BillDate,
                Memo = original.Memo,
                InvoiceNum = original.InvoiceNum,
                Lines = original.Lines.Select(l => new ServiceBillLine(l.AccountName, l.Amount)).ToList()
            };
        }
    }
}
