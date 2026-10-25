using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shop.Web.Services;

namespace Shop.Web.Controllers.Api;

public record CartAddRequest(int ProductId, int Quantity = 1);
public record CartUpdateRequest(int ProductId, int Quantity);
public record CartRemoveRequest(int ProductId);

/// <summary>
/// AJAX-корзина: добавление/изменение/удаление без перезагрузки страницы.
/// Все POST-запросы защищены Antiforgery-токеном (заголовок RequestVerificationToken из cart.js).
/// </summary>
[ApiController]
[Route("api/cart")]
[EnableRateLimiting("api")]
public class CartApiController : ControllerBase
{
    private readonly ICartService _cart;

    public CartApiController(ICartService cart) => _cart = cart;

    [HttpGet]
    public IActionResult Get() => Ok(CartState());

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add([FromBody] CartAddRequest request, CancellationToken ct)
    {
        var (ok, message) = await _cart.AddAsync(request.ProductId, request.Quantity, ct);
        if (!ok)
        {
            return BadRequest(new { ok = false, message });
        }

        return Ok(new { ok = true, message, cart = CartState() });
    }

    [HttpPost("update")]
    [ValidateAntiForgeryToken]
    public IActionResult Update([FromBody] CartUpdateRequest request)
    {
        _cart.UpdateQuantity(request.ProductId, request.Quantity);
        return Ok(new { ok = true, cart = CartState() });
    }

    [HttpPost("remove")]
    [ValidateAntiForgeryToken]
    public IActionResult Remove([FromBody] CartRemoveRequest request)
    {
        _cart.Remove(request.ProductId);
        return Ok(new { ok = true, cart = CartState() });
    }

    private object CartState()
    {
        var items = _cart.GetItems();
        return new
        {
            items = items.Select(i => new
            {
                i.ProductId,
                i.Name,
                i.Price,
                i.ImageUrl,
                i.Quantity,
                i.LineTotal
            }),
            totalQuantity = items.Sum(i => i.Quantity),
            total = items.Sum(i => i.LineTotal)
        };
    }
}
