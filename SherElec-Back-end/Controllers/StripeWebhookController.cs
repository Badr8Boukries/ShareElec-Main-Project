using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SherElec_Back_end.Services.Interfaces;
using Stripe; 
using Stripe.Checkout;
using System.IO;
using System.Threading.Tasks;
using SherElec_Back_end.DTOs.Request;
using System;


[Route("api/stripe")]
[ApiController]
public class StripeWebhookController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly string _webhookSecret; // À injecter depuis la configuration sécurisée !

    public StripeWebhookController(ITransactionService transactionService, IConfiguration configuration)
    {
        _transactionService = transactionService;
        // Récupérer la clé depuis la config (appsettings.json, user secrets, etc.)
        _webhookSecret = configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrEmpty(_webhookSecret))
        {
            throw new ArgumentNullException("Stripe Webhook Secret non configuré !");
        }
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Index()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        try
        {
            // Utilisez la clé secrète du webhook récupérée depuis la configuration
            var stripeEvent = EventUtility.ConstructEvent(json,
                Request.Headers["Stripe-Signature"], _webhookSecret);


            if (stripeEvent.Type == "checkout.session.completed")
            {
                var session = stripeEvent.Data.Object as Session;

                // Vérifier si le paiement a bien été effectué
                if (session.PaymentStatus == "paid")
                {
                    Console.WriteLine("✅ Paiement Stripe réussi ! Session ID: " + session.Id);

                    // Récupérer les métadonnées
                    var metadata = session.Metadata;
                    if (metadata != null &&
                        metadata.TryGetValue("acheteurId", out var acheteurIdStr) &&
                        metadata.TryGetValue("vendeurId", out var vendeurIdStr) &&
                        metadata.TryGetValue("offreId", out var offreIdStr) &&
                        metadata.TryGetValue("quantite", out var quantiteStr) &&
                        metadata.TryGetValue("amount", out var amountStr))
                    {
                        Console.WriteLine("📦 Métadonnées récupérées :");
                        Console.WriteLine($"  Acheteur ID: {acheteurIdStr}");
                        Console.WriteLine($"  Vendeur ID: {vendeurIdStr}");
                        Console.WriteLine($"  Offre ID: {offreIdStr}");
                        Console.WriteLine($"  Quantité: {quantiteStr}");
                        Console.WriteLine($"  Montant: {amountStr}");

                        try
                        {
                            // Correction 2 : Assurez-vous que TransactionRequest.Quantite et .Amount sont double
                            var transactionRequest = new TransactionRequest
                            {
                                AcheteurId = int.Parse(acheteurIdStr),
                                VendeurId = int.Parse(vendeurIdStr),
                                OffreId = !string.IsNullOrEmpty(offreIdStr) && offreIdStr != "0" ? int.Parse(offreIdStr) : 0,

                                Quantite = double.Parse(quantiteStr, System.Globalization.CultureInfo.InvariantCulture),
                                Amount = double.Parse(amountStr, System.Globalization.CultureInfo.InvariantCulture)
                            };

                            Console.WriteLine($"⚙️ Appel de CreateTransactionAsync...");
                            await _transactionService.CreateTransactionAsync(transactionRequest);
                            Console.WriteLine("✔️ Transaction enregistrée dans la base de données.");
                        }
                        catch (FormatException ex)
                        {
                            Console.Error.WriteLine($"❌ Erreur de format lors de la conversion des métadonnées: {ex.Message}");
                            return BadRequest("Erreur de format dans les métadonnées.");
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"❌ Erreur lors de l'appel à CreateTransactionAsync: {ex}");
                            return StatusCode(500, "Erreur interne lors du traitement de la transaction.");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("❌ Métadonnées manquantes ou incomplètes dans l'événement Stripe.");
                        return BadRequest("Métadonnées manquantes.");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ Statut de paiement non 'paid': {session.PaymentStatus}");
                }
            }
            else
            {
                Console.WriteLine($"🤷 Événement non géré : {stripeEvent.Type}");
            }

            return Ok();
        }
        catch (StripeException e)
        {
            Console.Error.WriteLine($"❌ Erreur Stripe (signature invalide ?) : {e.Message}");
            return BadRequest();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"❌ Erreur inconnue dans le webhook : {e.Message}");
            return StatusCode(500);
        }
    }
}