using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VentaMap.Data;
using VentaMap.Models;
using VentaMap.Services;

namespace VentaMap.Controllers;

[ApiController]
[Route("api/payments/mercadopago")]
public class MercadoPagoController(VentaMapDbContext db, CurrentUserAccessor currentUserAccessor, MercadoPagoService mercadoPagoService) : ControllerBase
{
    [Authorize]
    [HttpPost("checkout/{chargeId:int}")]
    public async Task<IActionResult> Checkout(int chargeId)
    {
        if (currentUserAccessor.UserId is not int userId) return Unauthorized();
        var charge = await db.BillingCharges
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == chargeId && x.UserId == userId && x.Status == "Pending");
        if (charge is null) return NotFound(new { message = "No se encontró el cargo pendiente." });
        var checkout = await mercadoPagoService.CreateCheckoutUrlAsync(charge.Id, charge.Description, charge.Amount, charge.User?.Email);
        if (!checkout.IsConfigured)
        {
            return StatusCode(503, new { message = "Mercado Pago todavía no está configurado." });
        }

        return string.IsNullOrWhiteSpace(checkout.Url)
            ? StatusCode(502, new { message = "Mercado Pago no pudo iniciar el pago. Intentá nuevamente en unos minutos." })
            : Ok(new { checkoutUrl = checkout.Url });
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromQuery(Name = "data.id")] string? dataId, [FromQuery] string? id)
    {
        var orderId = dataId ?? id;
        if (string.IsNullOrWhiteSpace(orderId)) return Ok();
        if (!mercadoPagoService.IsValidWebhook(Request.Headers["x-signature"], Request.Headers["x-request-id"], orderId))
            return Unauthorized();

        var order = await mercadoPagoService.GetProcessedOrderAsync(orderId);
        if (!order.Processed || order.ChargeId <= 0) return Ok();
        var charge = await db.BillingCharges.FirstOrDefaultAsync(x => x.Id == order.ChargeId && x.Status == "Pending");
        if (charge is null) return Ok();
        charge.Status = "Paid";
        charge.PaidAtUtc = DateTime.UtcNow;
        charge.MercadoPagoPaymentId = order.PaymentId ?? orderId;
        if (charge.PublicationId is int publicationId)
        {
            var publication = await db.Publications.FirstOrDefaultAsync(x =>
                x.Id == publicationId && x.UserId == charge.UserId && x.Status == PublicationStatus.PendingPayment);
            if (publication is not null)
            {
                publication.Status = PublicationStatus.Active;
                publication.IsActive = true;
                publication.ExpiresAtUtc = DateTime.UtcNow.AddDays(30);
                charge.Status = "Used";
            }
        }
        await db.SaveChangesAsync();
        return Ok();
    }
}
