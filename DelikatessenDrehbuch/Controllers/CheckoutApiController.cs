using System.Collections.Generic;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stripe;
using Stripe.Checkout;


namespace DelikatessenDrehbuch.Controllers
{
    public class StripeOptions
    {
        public string option { get; set; }
    }

    namespace server.Controllers
    {
        public class Program
        {
            public static void Main(string[] args)
            {
                WebHost.CreateDefaultBuilder(args)
                  .UseUrls("http://0.0.0.0:4242")
                  .UseWebRoot("public")
                  .UseStartup<Program>()
                  .Build()
                  .Run();
            }
        }





        [Route("create-checkout-session")]
        [ApiController]
        public class CheckoutApiController : Controller
        {

            
            [HttpPost]
            public ActionResult Create()
            {

                var domain = "https://Delikatessen-Drehbuch.com";
                var options = new SessionCreateOptions
                {
                    LineItems = new List<SessionLineItemOptions>
                {
                  new SessionLineItemOptions
                  {
                    // Provide the exact Price ID (for example, pr_1234) of the product you want to sell
                    Price = "price_1PwWRzFspvIIGBtcyJZPNYVE",
                    Quantity = 1,
                  },
                },
                    Mode = "subscription",
                    //TODO: Bestetigungsseite eifügen
                    SuccessUrl = domain + "/Payment/Success",
                    CancelUrl = domain + "/cancel.html",
                    AutomaticTax = new SessionAutomaticTaxOptions { Enabled = true },
                };
                var service = new SessionService();
                Stripe.Checkout.Session session = service.Create(options);

                Response.Headers.Add("Location", session.Url);
                return new StatusCodeResult(303);
            }
        }
    }
}


