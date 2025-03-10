using QBFC16Lib;
namespace Lib
{
    public class QueryVendor
    {
        public static List<QBVendor> GetVendors()
        {

            //Create the session Manager object
            QBSessionManager sessionManager = new QBSessionManager();

            //Create the message set request object to hold our request
            IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
            requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

            //BuildBillQueryRq(requestMsgSet);

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
    }
}
