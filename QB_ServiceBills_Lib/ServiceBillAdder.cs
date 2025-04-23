using QB_ServiceBills_Lib;
using QBFC16Lib;
using Serilog;

namespace QB_ServiceBills_Lib
{
    public static class ServiceBillAdder
    {
        public static void AddServiceBills(List<ServiceBill> serviceBillsToAdd)
        {
            QBSessionManager sessionManager = null;

            try
            {
                sessionManager = new QBSessionManager();
                sessionManager.OpenConnection("", AppConfig.QB_APP_NAME);
                sessionManager.BeginSession("", ENOpenMode.omDontCare);

                // 🧠 Step 1: Cache existing (Vendor + Invoice#) to prevent duplicates
                var existingBills = ServiceBillReader.QueryAllServiceBills();
                var existingKeys = new HashSet<string>(
                    existingBills.Select(b => $"{b.VendorName}|{b.InvoiceNum}"),
                    StringComparer.OrdinalIgnoreCase
                );

                foreach (var serviceBill in serviceBillsToAdd)
                {
                    string billKey = $"{serviceBill.VendorName}|{serviceBill.InvoiceNum}";

                    if (existingKeys.Contains(billKey))
                    {
                        Log.Warning($"⚠️ Skipped: Bill already exists for '{serviceBill.VendorName}' with Invoice# '{serviceBill.InvoiceNum}'.");
                        continue; // ❌ Skip duplicates
                    }

                    EnsureVendorExists(sessionManager, serviceBill.VendorName);

                    var request = sessionManager.CreateMsgSetRequest("US", 16, 0);
                    request.Attributes.OnError = ENRqOnError.roeContinue;

                    IBillAdd billAdd = request.AppendBillAddRq();
                    billAdd.VendorRef.FullName.SetValue(serviceBill.VendorName);
                    billAdd.TxnDate.SetValue(serviceBill.BillDate);
                    billAdd.Memo.SetValue(serviceBill.Memo);
                    billAdd.RefNumber.SetValue(serviceBill.InvoiceNum);

                    foreach (var line in serviceBill.Lines)
                    {
                        var expenseLine = billAdd.ExpenseLineAddList.Append();
                        expenseLine.AccountRef.FullName.SetValue(line.AccountName);
                        expenseLine.Amount.SetValue(line.Amount);
                    }

                    var response = sessionManager.DoRequests(request);
                    var resp = response.ResponseList.GetAt(0);

                    if (resp.StatusCode != 0)
                        Log.Warning($"❌ Failed to add bill for '{serviceBill.VendorName}': {resp.StatusMessage}");
                    else
                    {
                        var billRet = resp.Detail as IBillRet;
                        string txnID = billRet?.TxnID?.GetValue();
                        serviceBill.QBID = txnID ?? "";

                        Log.Information($"✅ Bill added for '{serviceBill.VendorName}' | TxnID: {txnID}");
                        existingKeys.Add(billKey); // Add to set to prevent re-adding in the same run
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled exception during bill creation.");
            }
            finally
            {
                try
                {
                    sessionManager?.EndSession();
                    sessionManager?.CloseConnection();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error while closing QuickBooks session.");
                }
            }
        }

        private static void EnsureVendorExists(QBSessionManager sessionManager, string vendorName)
        {
            var request = sessionManager.CreateMsgSetRequest("US", 16, 0);
            var query = request.AppendVendorQueryRq();
            query.IncludeRetElementList.Add("Name");

            var response = sessionManager.DoRequests(request);
            var resp = response.ResponseList.GetAt(0);

            bool vendorExists = false;

            if (resp.StatusCode == 0 && resp.Detail is IVendorRetList vendorList)
            {
                for (int i = 0; i < vendorList.Count; i++)
                {
                    var existingVendorName = vendorList.GetAt(i)?.Name?.GetValue();
                    if (string.Equals(existingVendorName, vendorName, StringComparison.OrdinalIgnoreCase))
                    {
                        vendorExists = true;
                        break;
                    }
                }
            }

            if (!vendorExists)
            {
                Log.Information($"🆕 Vendor '{vendorName}' not found. Adding now...");

                var addRequest = sessionManager.CreateMsgSetRequest("US", 16, 0);
                var vendAdd = addRequest.AppendVendorAddRq();
                vendAdd.Name.SetValue(vendorName);

                var addResponse = sessionManager.DoRequests(addRequest);
                var addResp = addResponse.ResponseList.GetAt(0);

                if (addResp.StatusCode != 0)
                    throw new Exception($"❌ Failed to add vendor '{vendorName}': {addResp.StatusMessage}");

                Log.Information($"✅ Vendor '{vendorName}' successfully added.");
            }
        }
    }
}
