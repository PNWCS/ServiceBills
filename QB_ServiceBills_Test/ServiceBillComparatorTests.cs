using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Serilog;
using Xunit;
using QB_ServiceBills_Lib;          // ServiceBill, ServiceBillLine, ServiceBillStatus (provided in model lib)
using QBFC16Lib;                   // QuickBooks Desktop SDK
using static QB_ServiceBills_Test.CommonMethods;   // logging helpers, QB session wrapper, etc.

namespace QB_ServiceBill_Test
{
    [Collection("Sequential Tests")]
    public class ServiceBillComparatorTests
    {
        [Fact]
        public void CompareServiceBill_InMemoryScenario_And_VerifyStatusesAndLogs()
        {
            // ---------- 0.  HOUSE-KEEPING ----------
            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            var firstCompareResult  = new List<ServiceBill>();
            var secondCompareResult = new List<ServiceBill>();

            string vendorListId = string.Empty;
            string vendorName   = "TestVendor_" + Guid.NewGuid().ToString("N")[..8];
            string acctListId   = string.Empty;
            string expenseAcct  = "TestExpense_" + Guid.NewGuid().ToString("N")[..8];

            try
            {
                // ---------- 1.  CREATE DEPENDENCIES ----------
                using (var qb = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    vendorListId = AddVendor(qb, vendorName);
                    acctListId   = AddExpenseAccount(qb, expenseAcct);
                }

                // ---------- 2.  BUILD FIVE COMPANY-SIDE SERVICE BILLS ----------
                var initialBills = Enumerable.Range(0, 5).Select(i =>
                {
                    string invoiceNum = $"SB-{Guid.NewGuid():N}".Substring(0, 12);
                    double amount     = Math.Round(new Random().NextDouble() * 500 + 50, 2);

                    return new ServiceBill
                    {
                        QBID        = string.Empty,
                        VendorName  = vendorName,
                        BillDate    = DateTime.Today,
                        Memo        = $"AutoTest Memo #{i}",
                        InvoiceNum  = invoiceNum,
                        Lines       = new()
                        {
                            new ServiceBillLine(expenseAcct, amount)
                        }
                    };
                }).ToList();

                // ---------- 3.  FIRST COMPARE  – expect ALL ‘Added’ ----------
                firstCompareResult = ServiceBillComparator.CompareServiceBill(initialBills);

                foreach (var bill in firstCompareResult.Where(b =>
                            initialBills.Any(x => x.InvoiceNum == b.InvoiceNum)))
                {
                    Assert.Equal(ServiceBillStatus.Added, bill.Status);
                    Assert.False(string.IsNullOrWhiteSpace(bill.QBID));
                }

                // ---------- 4.  MUTATE LIST ----------
                var updatedBills  = new List<ServiceBill>(initialBills);
                var billToRemove  = updatedBills[0];          // will become ‘Missing’
                var billToModify  = updatedBills[1];          // will become ‘Different’

                updatedBills.Remove(billToRemove);
                billToModify.Memo += "_MOD";

                // ---------- 5.  SECOND COMPARE ----------
                secondCompareResult = ServiceBillComparator.CompareServiceBill(updatedBills);

                var byInvoice = secondCompareResult.ToDictionary(b => b.InvoiceNum);

                Assert.Equal(ServiceBillStatus.Missing,   byInvoice[billToRemove.InvoiceNum].Status);
                Assert.Equal(ServiceBillStatus.Different, byInvoice[billToModify.InvoiceNum].Status);

                foreach (var bill in updatedBills.Where(b => b.InvoiceNum != billToModify.InvoiceNum))
                    Assert.Equal(ServiceBillStatus.Unchanged, byInvoice[bill.InvoiceNum].Status);
            }
            finally
            {
                // ---------- 6.  CLEAN-UP ----------
                try
                {
                    using var qb = new QuickBooksSession(AppConfig.QB_APP_NAME);

                    foreach (var b in firstCompareResult.Where(b => !string.IsNullOrEmpty(b.QBID)))
                        DeleteBill(qb, b.QBID);

                    if (!string.IsNullOrEmpty(acctListId))   DeleteAccount(qb, acctListId);
                    if (!string.IsNullOrEmpty(vendorListId)) DeleteVendor(qb, vendorListId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"⚠️  Cleanup warning: {ex.Message}");
                }
            }

            // ---------- 7.  VERIFY LOG OUTPUT ----------
            EnsureLogFileClosed();
            string logFile = GetLatestLogFile();
            EnsureLogFileExists(logFile);

            string logs = File.ReadAllText(logFile);

            Assert.Contains("ServiceBillComparator Initialized", logs);
            Assert.Contains("ServiceBillComparator Completed",   logs);

            foreach (var bill in firstCompareResult.Concat(secondCompareResult))
                Assert.Contains($"ServiceBill {bill.InvoiceNum} is {bill.Status}.", logs);
        }

        // ---------------- QB helper methods (unchanged) ----------------
        private string AddVendor(QuickBooksSession qb, string name)        { /* ... */ }
        private string AddExpenseAccount(QuickBooksSession qb, string name){ /* ... */ }
        private void   DeleteBill(QuickBooksSession qb, string txnID)      { /* ... */ }
        private void   DeleteVendor(QuickBooksSession qb, string listID)   { /* ... */ }
        private void   DeleteAccount(QuickBooksSession qb, string listID)  { /* ... */ }
    }
}
