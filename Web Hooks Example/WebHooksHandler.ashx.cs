using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Json;
using System.IO;

namespace Web_Hooks_Example
{
    /// <summary>
    /// Summary description for WebHooksHandler
    /// </summary>
    public class WebHooksHandler : IHttpHandler
    {
        private HttpRequest request;
        private HttpResponse response;

        public void ProcessRequest(HttpContext context)
        {
            request = context.Request;
            response = context.Response;

            response.ContentType = "text/plain";

            if (request.HttpMethod != "POST")
            {
                response.Write("Invalid request type.");
                return;
            }

            // Rewind the input stream as it has already been read by the pipeline
            request.InputStream.Position = 0;
            var sr = new StreamReader(request.InputStream);
            var postedData = sr.ReadToEnd();
            sr.Close();

            try
            {
                var payload = JsonValue.Parse(postedData);

                // We are looking for two things:
                // - the event name
                // - the payload (which will be an array of objects)

                var eventName = String.Empty;
                if (payload.ContainsKey("event") && payload["event"] != null)
                {
                    eventName = payload["event"].ReadAs<string>();
                }

                switch (eventName)
                {
                    case "charge:pending":
                        HandlePending(payload);
                        break;

                    case "charge:retry":
                        HandleRetry(payload);
                        break;

                    case "charge:successful":
                        HandleSuccessful(payload);
                        break;

                    case "charge:failed":
                        HandleFailed(payload);
                        break;

                    case "card:expiring":
                        HandleExpiring(payload);
                        break;

                    case "card:expired":
                        HandleExpired(payload);
                        break;

                    default:
                        HandleDefault(payload);
                        break;
                }
            }
            catch (FormatException ex)
            {
                response.Write(String.Format("Format Exception - unable to parse JSON: {0}", ex.Message));
            }
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Handle a pending transaction event (transaction is queued to be processed)
        /// </summary>
        /// <param name="payload">JSON payload</param>
        public void HandlePending(JsonValue json)
        {
            string output = String.Empty; // This output is for example purposes only - you simply need to return HTTP 200 for the hooks to be considered complete.

            // Iterate through the items
            foreach (var item in (JsonArray)json["payload"])
            {
                // Item ID
                var subscriptionID = item["id"].ReadAs<string>();
                var customerID = item["customer"]["id"].ReadAs<string>();

                // Use the above to send notification emails (e.g. Your about to be charged).
                // At this point the subscription is queued ready to go and cannot be cancelled, this is simply a FYI.
                output += String.Format("Notified Customer {0}\r\n", customerID);
            }

            response.Write(output);
        }

        /// <summary>
        /// Handle a retry transaction event (transaction has failed and is queued to be retried)
        /// </summary>
        /// <param name="payload">JSON payload</param>
        public void HandleRetry(JsonValue json)
        {
            string output = String.Empty;

            // Iterate through the items
            foreach (var item in (JsonArray)json["payload"])
            {
                // There are two objects here - a subscription, and a response (purchase)
                var subscription = item["subscription"]["id"].ReadAs<string>();
                var transaction = item["response"]; // This can be read in as a FatZebra.Purchase - FatZebra.Response.ParsePurchase(item["response"]) 

                output += String.Format("Purchase for {0} queued for retry.", subscription);
            }

            response.Write(output);
        }

        /// <summary>
        /// Handle a successful transaction event
        /// </summary>
        /// <param name="payload">JSON payload</param>
        public void HandleSuccessful(JsonValue json)
        {
            string output = String.Empty;

            // Iterate through the items
            foreach (var item in (JsonArray)json["payload"])
            {
                // There are two objects here - a subscription, and a purchase
                var subscription = item["subscription"]["id"].ReadAs<string>();
                var transaction  = item["response"]; // This can be read in as a FatZebra.Purchase - FatZebra.Response.ParsePurchase(item["response"]) 

                output += String.Format("Purchase for {0} successful, queued for next cycle.", subscription);
            }

            response.Write(output);
        }

        /// <summary>
        /// Handle a failed transaction event
        /// </summary>
        /// <param name="payload">JSON payload</param>
        public void HandleFailed(JsonValue json)
        {
            string output = String.Empty;

            // Iterate through the items
            foreach (var item in (JsonArray)json["payload"])
            {
                // There are two objects here - a subscription, and a response
                var subscription = item["subscription"]["id"].ReadAs<string>();
                var transaction = item["response"]; // This can be read in as a FatZebra.Purchase - FatZebra.Purchase.Parse(item["purchase"]) 

                output += String.Format("Purchase for {0} failed, abandoned.", subscription);
            }

            response.Write(output);
        }

        /// <summary>
        /// Handle an expiring card event
        /// </summary>
        /// <param name="payload">JSON payload</param>
        public void HandleExpiring(JsonValue json)
        {
            string output = String.Empty;

            // Iterate through the items
            foreach (var item in (JsonArray)json["payload"])
            {
                var customer = item["id"].ReadAs<string>();

                output += String.Format("Card for customer #{0} expiring within 30 days.", customer);
            }

            response.Write(output);
        }

        /// <summary>
        /// Handle an expired card event
        /// </summary>
        /// <param name="payload">JSON payload</param>
        public void HandleExpired(JsonValue json)
        {
            string output = String.Empty;

            // Iterate through the items
            foreach (var item in (JsonArray)json["payload"])
            {
                var customer = item["id"].ReadAs<string>();

                output += String.Format("Card for customer #{0} expired.", customer);
            }

            response.Write(output);
        }

        /// <summary>
        /// Handle an unknown event (default)
        /// </summary>
        /// <param name="payload">JSON payload</param>
        public void HandleDefault(JsonValue json)
        {
            response.Write(String.Format("Unknown event. Raw data: {0}", json.ToString()));
            response.StatusCode = 500;
        }
    }
}