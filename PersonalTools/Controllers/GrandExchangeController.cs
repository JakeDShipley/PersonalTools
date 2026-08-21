using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalTools.Classes.GrandExchange;
using PersonalTools.Entities.GrandExchange;

namespace PersonalTools.Controllers;

[Authorize]
[ApiController]
[Route("api/grand-exchange")]
public sealed class GrandExchangeController : ControllerBase
{
    private readonly IGrandExchangeFuncs _grandExchange;
    public GrandExchangeController(IGrandExchangeFuncs grandExchange) => _grandExchange = grandExchange;
    [HttpGet("search")] public async Task<ActionResult<GrandExchangeLookupResultObj>> Search([FromQuery] string term) => Ok(await _grandExchange.SearchItems(term));
}
