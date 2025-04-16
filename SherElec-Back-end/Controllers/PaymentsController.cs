namespace SherElec_Back_end.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using SherElec_Back_end.DTOs.Request;
    using Stripe;
    using Stripe.Checkout;
    using System;
    using System.Threading.Tasks;

    [ApiController]
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly string _stripeSecretKey = "sk_test_51R4SW0PENFnTPu7Q5LkDuMRp9Cr5zMNTuSfAtJiD60FdHNF0uXTG7RXqbJZJdi2rFgzhui2DnKPM2LgyiF3Sfwuy00LjWeid04"; // Mets ta clé secrète ici

        public PaymentsController()
        {
            StripeConfiguration.ApiKey = _stripeSecretKey;
        }

        [HttpPost("create-payment-intent")]
        public async Task<IActionResult> CreatePaymentIntent([FromBody] TransactionRequest request)
        {
            Console.WriteLine($"🔹 Requête reçue : Amount = {request}");

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
        {
            new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "eur",
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = "Achat d'énergie"
                    },
                    UnitAmount = (long)(request.Amount * 100) // Convertir en centimes
                },
                Quantity = 1
            }
        },
                Mode = "payment",
                SuccessUrl = "http://localhost:4200/offre/offres",
                CancelUrl = "http://localhost:4200/cancel"
            };

            var service = new SessionService();
            Session session = await service.CreateAsync(options);

            return Ok(new { sessionId = session.Id });  // 🚀 Retourne sessionId, pas clientSecret
        }

    }



}