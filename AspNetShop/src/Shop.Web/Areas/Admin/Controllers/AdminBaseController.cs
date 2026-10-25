using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Shop.Web.Areas.Admin.Controllers;

/// <summary>Базовый контроллер админки: вся область Admin доступна только роли Admin.</summary>
[Area("Admin")]
[Authorize(Policy = "AdminOnly")]
public abstract class AdminBaseController : Controller
{
}
