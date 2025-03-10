using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using QBFC16Lib;

namespace ServiceBillsLib
{
    public class VendorQuery
    {
        public static List<QBVendor> GetVendors()
        {

            //Create the session Manager object
            QBSessionManager sessionManager = new QBSessionManager();

            //Create the message set request object to hold our request
            IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
            requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

            BuildBillQueryRq(requestMsgSet);

            //Connect to QuickBooks and begin a session
            sessionManager.OpenConnection("", "Sample Code from OSR");
            sessionManager.BeginSession("", ENOpenMode.omDontCare);

            //Send the request and get the response from QuickBooks
            IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

            //End the session and close the connection to QuickBooks
            sessionManager.EndSession();
            sessionManager.CloseConnection();


            List<QBVendor> vendors = WalkBillQueryRs(responseMsgSet);

            return vendors;
        }


        static void BuildBillQueryRq(IMsgSetRequest requestMsgSet)
        {
            // Create the BillQuery request
            IBillQuery BillQueryRq = requestMsgSet.AppendBillQueryRq();

            // Set the query to include line items (optional)
            BillQueryRq.IncludeLineItems.SetValue(true);

            // Set the query to include linked transactions (optional)
            BillQueryRq.IncludeLinkedTxns.SetValue(true);

            // Optionally, set a date range for the query
            // Example: Fetch bills from the last 30 days
            DateTime fromDate = DateTime.Now.AddDays(-30);
            DateTime toDate = DateTime.Now;

            //BillQueryRq.ORBillQuery.BillFilter.ORDateRangeFilter.TxnDateRangeFilter.FromTxnDate.SetValue(fromDate);
            //BillQueryRq.ORBillQuery.BillFilter.ORDateRangeFilter.TxnDateRangeFilter.ToTxnDate.SetValue(toDate);

            // Optionally, filter by vendor (if needed)
            // Example: Fetch bills for a specific vendor
            // BillQueryRq.ORBillQuery.BillFilter.EntityFilter.OREntityFilter.FullNameList.Add("Vendor Name");

            // Optionally, filter by account (if needed)
            // Example: Fetch bills for a specific account
            // BillQueryRq.ORBillQuery.BillFilter.AccountFilter.ORAccountFilter.FullNameList.Add("Account Name");

            // Optionally, set the maximum number of bills to return
            BillQueryRq.ORBillQuery.BillFilter.MaxReturned.SetValue(100); // Adjust as needed
        }



        static List<QBVendor> WalkBillQueryRs(IMsgSetResponse responseMsgSet)
        {

            if (responseMsgSet == null) return null;

            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null) return null;
            List<QBVendor> vendors = new List<QBVendor>();
            //if we sent only one request, there is only one response, we'll walk the list for this sample
            for (int i = 0; i < responseList.Count; i++)
            {
                IResponse response = responseList.GetAt(i);
                //check the status code of the response, 0=ok, >0 is warning
                if (response.StatusCode >= 0)
                {
                    //the request-specific response is in the details, make sure we have some
                    if (response.Detail != null)
                    {
                        //make sure the response is the type we're expecting
                        ENResponseType responseType = (ENResponseType)response.Type.GetValue();


                        if (responseType == ENResponseType.rtBillQueryRs)
                        {
                            //upcast to more specific type here, this is safe because we checked with response.Type check above
                            IBillRetList BillRet = (IBillRetList)response.Detail;

                            for (int j = 0; j < BillRet.Count; j++)
                            {
                                vendors.Add(WalkBillRet(BillRet.GetAt(j)));
                            }

                        }
                    }
                }
            }
            return vendors;
        }





        static QBVendor WalkBillRet(IBillRet BillRet)
        {
            if (BillRet == null) return null;



            string VendorName = (string)BillRet.VendorRef.FullName.GetValue();

            //Get value of TxnDate
            DateTime Date1 = (DateTime)BillRet.TxnDate.GetValue();
            //Get value of DueDate

            DateTime DueDate1766 = (DateTime)BillRet.DueDate.GetValue();

            //Get value of AmountDue
            double Amount = (double)BillRet.AmountDue.GetValue();
            string chartOfAccount = (string)BillRet.APAccountRef.FullName.GetValue();




            QBVendor qbVendor = new QBVendor(VendorName, Date1.ToString(), chartOfAccount, Amount.ToString());
            return qbVendor;
        }




        //        ----------------------------------------------------
        //            // TODO: Replace with actual QuickBooks API call to fetch vendors
        //            var vendors = QuickBooksAPI.GetVendors(); // Hypothetical API call

        //            // Convert the result into our defined structure
        //            return vendors.Select(v => new VendorInfo
        //            {
        //                Name = v.Name,
        //                Date = v.Date,
        //                ChartOfAccount = v.ChartOfAccount,
        //                Amount = v.Amount,
        //                Memo = v.Memo
        //            }).ToList();
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine("Error fetching vendors: " + ex.Message);
        //            return new List<VendorInfo>();
        //        }
        //    }
        //}

        //public class VendorInfo
        //{
        //    public string Name { get; set; }
        //    public string Date { get; set; }
        //    public string ChartOfAccount { get; set; }
        //    public decimal Amount { get; set; }
        //    public string Memo { get; set; }
        //}
    }
}