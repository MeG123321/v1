using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Magazyn.Controllers;

public partial class UzytkownicyController : Controller
{
    [HttpGet]
    [Authorize]
    public IActionResult MyAccount() => View();
}
