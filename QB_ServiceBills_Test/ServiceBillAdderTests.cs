using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Serilog;
using QBFC16Lib;
using QB_ServiceBills_Lib;

namespace QB_ServiceBills_Test
{
    [Collection("Sequential Tests")]
    public class ServiceBillAdderTests
    {
        private const int BILL_COUNT = 2;

        [Fact]
        public void AddServiceBills_SuccessfullyAddsToQB()
        {
            var createdVendorListIDs = new List<string>();
            // We'll create a few random vendors:
            var vendorNames = new List<string>();

            // We'll hold onto the bills we submit, so we can confirm their QBIDs and do cleanup.
            var bills = new List<ServiceBill>();

            try
            {
                // 1) Clean logs, etc.
                EnsureLogFileClosed();
                DeleteOldLogFiles();
                ResetLogger();

                // 2) Create random vendors
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    for (int i = 0; i < BILL_COUNT; i++)
                    {
                        string vendorName = "TestVend_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                        string vendorListID = AddVendor(qbSession, vendorName);
                        createdVendorListIDs.Add(vendorListID);
                        vendorNames.Add(vendorName);
                    }
                }

                // 3) Build some test ServiceBill objects
                //    Each ServiceBill references one of the vendors we just created.
                for (int i = 0; i < BILL_COUNT; i++)
                {
                    // We'll store a "company ID" in the Memo field (like the example).
                    // We'll set two lines for demonstration.
                    var sb = new ServiceBill
                    {
                        VendorName = vendorNames[i],
                        BillDate = DateTime.Today.AddDays(-i), // slightly different dates
                        Memo = (100 + i).ToString(),
                        InvoiceNum = "ServiceBill_" + (200 + i),
                        Lines = new List<ServiceBillLine>
                        {
                            new ServiceBillLine { AccountName = "Utilities", Amount = 25 + i },
                            new ServiceBillLine { AccountName = "Computer and Internet Expenses", Amount = 10 + i }
                        }
                    };
                    bills.Add(sb);
                }

                // 4) Call your library's Adder code
                //    This should fill in the QBID for each service bill if successful.
                ServiceBillAdder.AddServiceBills(bills);

                // 5) Verify each newly added bill has a QBID, then query QB to confirm it exists
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var bill in bills)
                    {
                        // If AddServiceBills didn't populate QBID, test fails immediately
                        Assert.False(string.IsNullOrWhiteSpace(bill.QBID),
                            $"ServiceBill for vendor '{bill.VendorName}' did not get a QBID assigned.");

                        // Now verify it's really in QuickBooks
                        bool foundInQB = CheckBillInQB(qbSession, bill.QBID);
                        Assert.True(foundInQB,
                            $"Could not find newly added ServiceBill in QB. TxnID (QBID) = {bill.QBID}");
                    }
                }
            }
            finally
            {
                // 6) Clean up: delete bills first, then vendors
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var bill in bills)
                    {
                        // If QBID is empty, skip.
                        if (!string.IsNullOrWhiteSpace(bill.QBID))
                        {
                            DeleteBill(qbSession, bill.QBID);
                        }
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

        //--------------------------------------------------------------------------------
        // HELPER: Add a Vendor
        //--------------------------------------------------------------------------------
        private string AddVendor(QuickBooksSession qbSession, string vendorName)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            IVendorAdd vendAdd = request.AppendVendorAddRq();

            vendAdd.Name.SetValue(vendorName);
            // Add any additional vendor fields as needed

            var resp = qbSession.SendRequest(request);
            return ExtractVendorListID(resp);
        }

        //--------------------------------------------------------------------------------
        // HELPER: Check if a Bill exists in QB by TxnID
        //         Returns true if found, false otherwise
        //--------------------------------------------------------------------------------
        private bool CheckBillInQB(QuickBooksSession qbSession, string txnID)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            IBillQuery billQuery = request.AppendBillQueryRq();
            // We only need to query for this single TxnID
            billQuery.ORBillQuery.TxnIDList.Add(txnID);

            // We can request line items, but not strictly necessary if
            // we just want to confirm the Bill exists:
            billQuery.IncludeLineItems.SetValue(true);

            IMsgSetResponse response = qbSession.SendRequest(request);
            var list = response.ResponseList;
            if (list == null || list.Count == 0) return false;

            var firstResp = list.GetAt(0);
            if (firstResp.StatusCode != 0) return false; // Some error or no match

            // If Bill was found, firstResp.Detail should be an IBillRetList containing the matching bill
            var billRet = firstResp.Detail as IBillRetList;
            if (billRet == null) return false;

            // If there's a valid BillRet, it exists
            return true;
        }

        //--------------------------------------------------------------------------------
        // HELPER: Delete Bill by TxnID
        //--------------------------------------------------------------------------------
        private void DeleteBill(QuickBooksSession qbSession, string txnID)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var delRq = request.AppendTxnDelRq();
            delRq.TxnDelType.SetValue(ENTxnDelType.tdtBill);
            delRq.TxnID.SetValue(txnID);

            var resp = qbSession.SendRequest(request);
            CheckForError(resp, $"Deleting ServiceBill TxnID={txnID}");
        }

        //--------------------------------------------------------------------------------
        // HELPER: Delete Vendors, etc.
        //--------------------------------------------------------------------------------
        private void DeleteListObject(QuickBooksSession qbSession, string listID, ENListDelType listDelType)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var listDel = request.AppendListDelRq();
            listDel.ListDelType.SetValue(listDelType);
            listDel.ListID.SetValue(listID);

            var resp = qbSession.SendRequest(request);
            CheckForError(resp, $"Deleting {listDelType} {listID}");
        }

        //--------------------------------------------------------------------------------
        // HELPER: Extract Vendor ListID from the response
        //--------------------------------------------------------------------------------
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

        //--------------------------------------------------------------------------------
        // HELPER: CheckForError
        //--------------------------------------------------------------------------------
        private void CheckForError(IMsgSetResponse resp, string context)
        {
            if (resp?.ResponseList == null || resp.ResponseList.Count == 0)
                return;

            var firstResp = resp.ResponseList.GetAt(0);
            if (firstResp.StatusCode != 0)
            {
                throw new Exception($"Error {context}: {firstResp.StatusMessage}. " +
                                    $"Status code: {firstResp.StatusCode}");
            }
            else
            {
                Debug.WriteLine($"OK: {context}");
            }
        }
    }
}
