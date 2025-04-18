using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration; 
using SherElec_Back_end.DTOs.Request;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic; 
using System.Globalization; 
using System.Text.Json; 
using System.Threading.Tasks;

namespace SherElec_Back_end.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly string _stripeSecretKey;
        private readonly string _clientAppUrl; 

        public PaymentsController(IConfiguration configuration)
        {
            _stripeSecretKey = configuration["Stripe:SecretKey"];
            _clientAppUrl = configuration["ClientAppUrl"] ?? "http://localhost:4200"; 

            if (string.IsNullOrEmpty(_stripeSecretKey))
            {
                // Gérer l'absence de clé
                Console.Error.WriteLine("ERREUR CRITIQUE : Clé secrète Stripe non configurée !");
            }
        }

        [HttpPost("create-payment-intent")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] TransactionRequest request)
        {
            if (request == null)
            {
                Console.Error.WriteLine("❌ Requête CreateCheckoutSession reçue avec un corps vide.");
                return BadRequest(new { message = "Les données de la transaction sont manquantes." });
            }
            if (request.Amount < 0.50 || request.Quantite <= 0)
            {
                Console.Error.WriteLine($"❌ Requête CreateCheckoutSession invalide reçue : Amount={request.Amount}, Quantite={request.Quantite}");
                return BadRequest(new { message = "Montant ou quantité invalide." });
            }

            try
            {
                Console.WriteLine($"🔹 Requête CreateCheckoutSession reçue : {JsonSerializer.Serialize(request)}");
            }
            catch (Exception jsonEx) { Console.Error.WriteLine($"Erreur logging requête: {jsonEx.Message}"); }


            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "eur", // Peut être configuré si besoin
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Achat de {request.Quantite} kWh d'énergie" // Nom plus descriptif
                            },
                            UnitAmount = (long)(request.Amount * 100) // Conversion en centimes
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",

                Metadata = new Dictionary<string, string>
                {
                    { "acheteurId", request.AcheteurId.ToString() },
                    { "vendeurId", request.VendeurId.ToString() },
                    { "offreId", (request.OffreId != 0 ? request.OffreId.ToString() : "0") }, 
                    { "quantite", request.Quantite.ToString(CultureInfo.InvariantCulture) }, 
                    { "amount", request.Amount.ToString(CultureInfo.InvariantCulture) }     
                },

                SuccessUrl = $"{_clientAppUrl}/offre/offres", 
                CancelUrl = $"{_clientAppUrl}/cancel"       
            };

            Console.WriteLine("⚙️ Options de Session Stripe préparées :");
            try
            {
                Console.WriteLine(JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception jsonEx) { Console.Error.WriteLine($"Erreur sérialisation options: {jsonEx.Message}"); }


            Session session = null; 
            try
            {
                var client = new StripeClient(_stripeSecretKey);
                var service = new SessionService(client); 

                Console.WriteLine("⏳ Appel à SessionService.CreateAsync...");
                session = await service.CreateAsync(options); 
                Console.WriteLine($"✅ Session Stripe créée avec succès ! ID: {session.Id}");
                return Ok(new { sessionId = session.Id });
            }
            catch (StripeException stripeEx) 
            {
                Console.Error.WriteLine($"❌ Erreur Stripe lors de la création de la session : {stripeEx.StripeError?.Code} - {stripeEx.StripeError?.Message ?? stripeEx.Message}");
                Console.Error.WriteLine($"  Stripe Request ID: {stripeEx.StripeResponse?.RequestId}");
                return StatusCode(502, new
                { 
                    message = $"Erreur de communication avec le service de paiement: {stripeEx.StripeError?.Message ?? "Détails indisponibles"}",
                    stripeErrorCode = stripeEx.StripeError?.Code
                });
            }
            catch (Exception ex) // Attrape toute autre erreur inattendue
            {
                Console.Error.WriteLine($"❌ Erreur inattendue lors de la création de la session Stripe : {ex.Message}");
                // Log complet de l'exception pour le diagnostic interne
                Console.Error.WriteLine(ex.ToString());
                return StatusCode(500, new { message = "Une erreur interne est survenue lors de la préparation du paiement." });
            }
        }
    }
}