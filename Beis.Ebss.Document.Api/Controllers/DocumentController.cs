using Microsoft.AspNetCore.Mvc;

namespace Beis.Ebss.Document.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(ILogger<DocumentController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "Get")]
    public IEnumerable<string> Get()
    {
        this._logger.LogInformation("DocumentController:Get");
        return Enumerable.Range(1, 5).Select(index => new string($"Documents {index}"))
            .ToArray();
    }
}