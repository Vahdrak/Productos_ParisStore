using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace ComprasParaguai.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ComprasParaguaiController : ControllerBase
{
    private readonly ILogger<ComprasParaguaiController> _logger;

    public ComprasParaguaiController(ILogger<ComprasParaguaiController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// GET /api/comprasparaguai/products?token=...
    /// Proxy a la API de Compras Paraguai para listar productos.
    /// </summary>
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts([FromQuery] string token, [FromQuery] string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
            return BadRequest(new { message = "El token es obligatorio" });

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("token", token);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            string url = "https://products.comprasparaguai.com.br/api/products/list/";
            if (!string.IsNullOrEmpty(code))
            {
                url += $"?code={Uri.EscapeDataString(code)}";
            }

            _logger.LogInformation("Realizando petición GET a Compras Paraguai con token provisto");
            var response = await client.GetAsync(url, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<object>(content);
                return Ok(data);
            }

            return StatusCode((int)response.StatusCode, new { error = content });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consultando listado en Compras Paraguai");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/comprasparaguai/import?token=...
    /// Proxy a la API de Compras Paraguai para importar/sincronizar productos.
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> ImportProducts([FromQuery] string token, [FromBody] object payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
            return BadRequest(new { message = "El token es obligatorio" });

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("token", token);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            _logger.LogInformation("Realizando petición POST a Compras Paraguai con token provisto");
            var response = await client.PostAsJsonAsync("https://products.comprasparaguai.com.br/api/products/import/", payload, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode || (int)response.StatusCode == 207)
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<object>(content);
                return Ok(data);
            }

            return StatusCode((int)response.StatusCode, new { error = content });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importando lote a Compras Paraguai");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
