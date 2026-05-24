using Microsoft.AspNetCore.Mvc;
using Restaurantes.Web.Models;
using Restaurantes.Web.Services;

namespace Restaurantes.Web.Controllers;

[ApiController]
[Route("api/public/restaurants/{restaurantId:guid}")]
public sealed class ApiPublicRestaurantsController : ControllerBase
{
    private readonly RestaurantService _restaurantService;
    private readonly PublicOrderPaymentService _publicOrderPaymentService;

    public ApiPublicRestaurantsController(
        RestaurantService restaurantService,
        PublicOrderPaymentService publicOrderPaymentService)
    {
        _restaurantService = restaurantService;
        _publicOrderPaymentService = publicOrderPaymentService;
    }

    [HttpGet("table-session")]
    public async Task<IActionResult> TableSession(Guid restaurantId, Guid tableId)
    {
        var session = await _restaurantService.GetPublicRestaurantTableSessionAsync(restaurantId, tableId);
        return session is null
            ? Ok(new { valid = false, hasOpenTab = false })
            : Ok(new { valid = true, session.TableId, session.TableNumber, session.HasOpenTab });
    }

    [HttpPost("order")]
    public async Task<IActionResult> Order(Guid restaurantId, PublicOrderSubmissionInput input)
    {
        if (input.RestaurantId != restaurantId)
        {
            return BadRequest(new { error = "Revise a mesa e os itens do pedido antes de enviar." });
        }

        try
        {
            var result = await _publicOrderPaymentService.SubmitPublicOrderAsync(input, HttpContext.RequestAborted);
            return Ok(new { message = "Pedido enviado.", data = result });
        }
        catch (InvalidOperationException error)
        {
            return BadRequest(new { error = error.Message });
        }
    }

    [HttpPost("coupon/validate")]
    public async Task<IActionResult> ValidateCoupon(Guid restaurantId, PublicCouponValidationInput input)
    {
        if (input.RestaurantId != restaurantId)
        {
            return BadRequest(new { error = "Revise os itens antes de aplicar o cupom." });
        }

        try
        {
            var result = await _restaurantService.ValidatePublicCouponAsync(input);
            return Ok(new { message = "Cupom aplicado.", data = result });
        }
        catch (InvalidOperationException error)
        {
            return BadRequest(new { error = error.Message });
        }
    }

    [HttpPost("service-request")]
    public async Task<IActionResult> ServiceRequest(Guid restaurantId, PublicServiceRequestInput input)
    {
        if (input.RestaurantId != restaurantId)
        {
            return BadRequest(new { error = "Revise a mesa antes de enviar a solicitação." });
        }

        try
        {
            var result = await _restaurantService.CreatePublicServiceRequestAsync(input);
            return Ok(new { message = "Solicitação enviada.", data = result });
        }
        catch (InvalidOperationException error)
        {
            return BadRequest(new { error = error.Message });
        }
    }

    [HttpPost("feedback")]
    public async Task<IActionResult> Feedback(Guid restaurantId, PublicOrderFeedbackInput input)
    {
        try
        {
            var result = await _restaurantService.SubmitPublicOrderFeedbackAsync(restaurantId, input);
            return Ok(new { message = "Obrigado pelo feedback.", data = result });
        }
        catch (InvalidOperationException error)
        {
            return BadRequest(new { error = error.Message });
        }
    }
}
