using ComprasParaguai.Api.Auth;
using ComprasParaguai.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComprasParaguai.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUsuarioRepository _usuarios;
    private readonly AuthService _auth;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUsuarioRepository usuarios, AuthService auth, ILogger<AuthController> logger)
    {
        _usuarios = usuarios;
        _auth = auth;
        _logger = logger;
    }

    public record LoginRequest(string? Usuario, string? Password);

    /// <summary>
    /// Inicia sesión. POST /api/auth/login  { "usuario": "...", "password": "..." }
    /// Devuelve { token, usuario, nombre, vence }. El token va luego en el header Authorization: Bearer.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Usuario) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new { error = "Usuario y contraseña son obligatorios" });

        try
        {
            var usuario = await _usuarios.GetByLoginAsync(request.Usuario.Trim(), cancellationToken);

            if (!_auth.Verificar(usuario, request.Password))
            {
                _logger.LogWarning("Login fallido para el usuario {Usuario}", request.Usuario.Trim());
                await Task.Delay(400, cancellationToken);   // frena la fuerza bruta
                return Unauthorized(new { error = "Usuario o contraseña incorrectos" });
            }

            await _usuarios.RegistrarLoginAsync(usuario!.Id, cancellationToken);
            var (token, vence) = _auth.CrearToken(usuario);
            return Ok(new { token, usuario = usuario.Login, nombre = usuario.Nombre, vence });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en el login");
            return StatusCode(500, new { error = "Error iniciando sesión" });
        }
    }

    /// <summary>Datos de la sesión actual. GET /api/auth/me (requiere token).</summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me() => Ok(new
    {
        usuario = User.Identity?.Name,
        nombre = User.FindFirst(AuthService.ClaimNombre)?.Value
    });
}
