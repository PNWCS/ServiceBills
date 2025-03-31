using System.Diagnostics;
using Serilog;
using QBFC16Lib;
using static QB_ServiceBills_Test.CommonMethods; // Reuse your shared helpers
using QB_ServiceBills_Lib;

namespace QB_ServiceBills_Test
{
    [Collection("Sequential Tests")]
    public class ServiceBillReaderTests
    {
        private const int BILL_COUNT = 2; // We'll create 2 vendors, and 2 bills

        [Fact]
        public void CreateAndDelete_Vendors_ServiceBills()
        {
            var createdVendorListIDs = new List<string>();
            var createdBillTxnIDs = new List<string>();

            // We'll store random vendor names.
            var randomVendorNames = new List<string>();

            // We'll store test data for the bills, to verify after reading
            var serviceBillTestData = new List<ServiceBillTestInfo>();

            try
            {
                // 1) Clean logs, etc.
                EnsureLogFileClosed();
                DeleteOldLogFiles();
                ResetLogger();

                // 2) Create 2 vendors
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    for (int i = 0; i < BILL_COUNT; i++)
                    {
                        string vendorName = "RandVend_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                        string vendorListID = AddVendor(qbSession, vendorName);
                        createdVendorListIDs.Add(vendorListID);
                        randomVendorNames.Add(vendorName);
                    }
                }

                // 3) Create 2 bills, each with 2 expense lines
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    for (int i = 0; i < BILL_COUNT; i++)
                    {
                        string vendorListID = createdVendorListIDs[i];
                        string vendorName = randomVendorNames[i];

                        // We'll store the numeric CompanyID in the memo, e.g. 100 + i
                        int companyID = 100 + i;
                        DateTime billDate = DateTime.Today;
                        string vendorInvoiceNum = "SBILL_" + (200 + i); // e.g. "SBILL_200", "SBILL_201"

                        // We'll pick 2 expense accounts for demonstration; adapt to your QB file.
                        // For example: "Utilities" and "Internet Expense"
                        // We'll also choose amounts.
                        string expAcct1 = "Utilities";
                        double amt1 = 30.0 + i;
                        string expAcct2 = "Computer and Internet Expenses";
                        double amt2 = 15.0 + i;

                        string billTxnID = AddServiceBill(
                            qbSession,
                            vendorListID,
                            vendorName,
                            billDate,
                            vendorInvoiceNum,
                            companyID,
                            expAcct1,
                            amt1,
                            expAcct2,
                            amt2
                        );

                        createdBillTxnIDs.Add(billTxnID);

                        // Save data for final checks
                        serviceBillTestData.Add(new ServiceBillTestInfo
                        {
                            TxnID = billTxnID,
                            CompanyID = companyID,
                            VendorName = vendorName,
                            BillDate = billDate,
                            RefNumber = vendorInvoiceNum,
                            Lines = new List<ServiceBillLine>
                            {
                                new ServiceBillLine { AccountName = expAcct1, Amount = amt1 },
                                new ServiceBillLine { AccountName = expAcct2, Amount = amt2 }
                            }
                        });
                    }
                }

                // 4) Query & verify
                var allServiceBills = ServiceBillReader.QueryAllServiceBills();
                // This is your custom method returning a list of ServiceBills from QuickBooks.

                foreach (var testBill in serviceBillTestData)
                {
                    var matchingBill = allServiceBills.FirstOrDefault(x => x.QBID == testBill.TxnID);
                    Assert.NotNull(matchingBill);

                    // Check memo (numeric CompanyID)
                    Assert.Equal(testBill.CompanyID.ToString(), matchingBill.Memo);
                    // Check vendor
                    Assert.Equal(testBill.VendorName, matchingBill.VendorName);
                    // Check date
                    Assert.Equal(testBill.BillDate.Date, matchingBill.BillDate.Date);
                    // Check vendor invoice number
                    Assert.Equal(testBill.RefNumber, matchingBill.InvoiceNum);

                    // Check lines
                    Assert.Equal(2, matchingBill.Lines.Count);

                    Assert.Equal(testBill.Lines[0].AccountName, matchingBill.Lines[0].AccountName);
                    Assert.Equal(testBill.Lines[0].Amount, matchingBill.Lines[0].Amount);

                    Assert.Equal(testBill.Lines[1].AccountName, matchingBill.Lines[1].AccountName);
                    Assert.Equal(testBill.Lines[1].Amount, matchingBill.Lines[1].Amount);
                }
            }
            finally
            {
                // 5) Cleanup: delete bills first, then vendors
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var billID in createdBillTxnIDs)
                    {
                        DeleteBill(qbSession, billID);
                    }
                }

                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var vendID in createdVendorListIDs)
                    {
                        DeleteListObject(qbSession, vendID, ENListDelType.ldtVendor);
                    }
                }
            }
        }

        //------------------------------------------------------------------------------
        // AddVendor
        //------------------------------------------------------------------------------

        private string AddVendor(QuickBooksSession qbSession, string vendorName)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            IVendorAdd vendAdd = request.AppendVendorAddRq();

            vendAdd.Name.SetValue(vendorName);
            // Additional vendor fields as needed

            var resp = qbSession.SendRequest(request);
            return ExtractVendorListID(resp);
        }

        //------------------------------------------------------------------------------
        // Create the Bill with Expense Lines
        //------------------------------------------------------------------------------

        private string AddServiceBill(
            QuickBooksSession qbSession,
            string vendorListID,
            string vendorName,
            DateTime billDate,
            string vendorInvoiceNum,
            int companyID,
            string expAcct1,
            double amt1,
            string expAcct2,
            double amt2
        )
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            IBillAdd billAddRq = request.AppendBillAddRq();

            // Required: the vendor
            billAddRq.VendorRef.ListID.SetValue(vendorListID);

            // Bill date
            billAddRq.TxnDate.SetValue(billDate);

            // Vendor invoice number
            billAddRq.RefNumber.SetValue(vendorInvoiceNum);

            // Our numeric ID in the Bill's memo
            billAddRq.Memo.SetValue(companyID.ToString());

            // Add 2 expense lines
            // 1) first line
            var expLine1 = billAddRq.ExpenseLineAddList.Append();
            expLine1.AccountRef.FullName.SetValue(expAcct1);
            expLine1.Amount.SetValue(amt1);
            // Optionally: expLine1.Memo.SetValue(...)

            // 2) second line
            var expLine2 = billAddRq.ExpenseLineAddList.Append();
            expLine2.AccountRef.FullName.SetValue(expAcct2);
            expLine2.Amount.SetValue(amt2);

            // Send to QB
            var resp = qbSession.SendRequest(request);
            return ExtractBillTxnID(resp);
        }

        //------------------------------------------------------------------------------
        // Deletion
        //------------------------------------------------------------------------------

        private void DeleteBill(QuickBooksSession qbSession, string txnID)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var delRq = request.AppendTxnDelRq();
            delRq.TxnDelType.SetValue(ENTxnDelType.tdtBill);
            delRq.TxnID.SetValue(txnID);

            var resp = qbSession.SendRequest(request);
            CheckForError(resp, $"Deleting ServiceBill TxnID={txnID}");
        }

        private void DeleteListObject(QuickBooksSession qbSession, string listID, ENListDelType listDelType)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var listDel = request.AppendListDelRq();
            listDel.ListDelType.SetValue(listDelType);
            listDel.ListID.SetValue(listID);

            var resp = qbSession.SendRequest(request);
            CheckForError(resp, $"Deleting {listDelType} {listID}");
        }

        //------------------------------------------------------------------------------
        // Extractors
        //------------------------------------------------------------------------------

        private string ExtractVendorListID(IMsgSetResponse resp)
        {
            var list = resp.ResponseList;
            if (list == null || list.Count == 0)
                throw new Exception("No response from VendorAdd.");

            var firstResp = list.GetAt(0);
            if (firstResp.StatusCode != 0)
                throw new Exception($"VendorAdd failed: {firstResp.StatusMessage}");

            var vendRet = firstResp.Detail as IVendorRet;
            if (vendRet == null)
                throw new Exception("No IVendorRet returned.");

            return vendRet.ListID.GetValue();
        }

        private string ExtractBillTxnID(IMsgSetResponse resp)
        {
            var list = resp.ResponseList;
            if (list == null || list.Count == 0)
                throw new Exception("No response from BillAdd.");

            var firstResp = list.GetAt(0);
            if (firstResp.StatusCode != 0)
                throw new Exception($"BillAdd failed: {firstResp.StatusMessage}");

            var billRet = firstResp.Detail as IBillRet;
            if (billRet == null)
                throw new Exception("No IBillRet returned.");

            return billRet.TxnID.GetValue();
        }

        //------------------------------------------------------------------------------
        // Error Handler
        //------------------------------------------------------------------------------

        private void CheckForError(IMsgSetResponse resp, string context)
        {
            if (resp?.ResponseList == null || resp.ResponseList.Count == 0)
                return;

            var firstResp = resp.ResponseList.GetAt(0);
            if (firstResp.StatusCode != 0)
            {
                throw new Exception($"Error {context}: {firstResp.StatusMessage}. Status code: {firstResp.StatusCode}");
            }
            else
            {
                Debug.WriteLine($"OK: {context}");
            }
        }

        //------------------------------------------------------------------------------
        // POCO classes
        //------------------------------------------------------------------------------

        private class ServiceBillTestInfo
        {
            public string TxnID { get; set; } = "";
            public int CompanyID { get; set; }
            public string VendorName { get; set; } = "";
            public DateTime BillDate { get; set; }
            public string RefNumber { get; set; } = "";
            public List<ServiceBillLine> Lines { get; set; } = new();
        }

        private class ServiceBillLine
        {
            public string AccountName { get; set; } = "";
            public double Amount { get; set; }
        }
    }
}
