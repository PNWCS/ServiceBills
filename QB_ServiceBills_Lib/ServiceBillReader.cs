using Microsoft.VisualBasic;
using QBFC16Lib;
using Serilog;

namespace QB_ServiceBills_Lib
{
    public class ServiceBillReader
    {
        public static List<ServiceBill> QueryAllServiceBills()
        {
            var serviceBills = new List<ServiceBill>();

            try
            {
                QBSessionManager sessionManager = new QBSessionManager();

                IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
                requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

                var billQueryRq = requestMsgSet.AppendBillQueryRq();
                billQueryRq.IncludeLineItems.SetValue(true); // Important
                billQueryRq.IncludeLinkedTxns.SetValue(false);

                // Optional: Limit max
                billQueryRq.ORBillQuery.BillFilter.MaxReturned.SetValue(100);

                sessionManager.OpenConnection("", AppConfig.QB_APP_NAME);
                sessionManager.BeginSession("", ENOpenMode.omDontCare);

                IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

                sessionManager.EndSession();
                sessionManager.CloseConnection();

                var list = responseMsgSet?.ResponseList;
                if (list == null || list.Count == 0)
                    return serviceBills;

                IResponse billResp = list.GetAt(0);
                if (billResp.StatusCode != 0 || billResp.Detail == null)
                    return serviceBills;

                IBillRetList billList = billResp.Detail as IBillRetList;
                if (billList == null || billList.Count == 0)
                    return serviceBills;

                for (int i = 0; i < billList.Count; i++)
                {
                    var bill = billList.GetAt(i);
                    var serviceBill = new ServiceBill
                    {
                        QBID = bill.TxnID?.GetValue() ?? "",
                        VendorName = bill.VendorRef?.FullName?.GetValue() ?? "",
                        BillDate = bill.TxnDate?.GetValue() ?? DateTime.MinValue,
                        Memo = bill.Memo?.GetValue() ?? "",
                        InvoiceNum = bill.RefNumber?.GetValue() ?? "",
                        Lines = new List<ServiceBillLine>()
                    };

                    var expLines = bill.ExpenseLineRetList;
                    if (expLines != null)
                    {
                        for (int j = 0; j < expLines.Count; j++)
                        {
                            var exp = expLines.GetAt(j);
                            var account = exp.AccountRef?.FullName?.GetValue() ?? "";
                            var amount = exp.Amount?.GetValue() ?? 0.0;

                            serviceBill.Lines.Add(new ServiceBillLine
                            {
                                AccountName = account,
                                Amount = amount
                            });
                        }
                    }

                    serviceBills.Add(serviceBill);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while querying service bills from QuickBooks.");
            }

            return serviceBills;
        }
    }
}