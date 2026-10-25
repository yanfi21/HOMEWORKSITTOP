namespace Shop.Web.Models;

/// <summary>
/// Позиция корзины. Корзина хранится в сессии (Session -> IDistributedCache),
/// поэтому работает и для гостя, и для авторизованного пользователя,
/// а при горизонтальном масштабировании кэш заменяется на Redis без изменения кода.
/// </summary>
public class CartItem
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;

    public decimal LineTotal => Price * Quantity;
}
