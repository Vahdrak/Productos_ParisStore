using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ComprasParaguai.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace ComprasParaguai.Api.Auth;

/// <summary>Opciones de autenticación (sección Auth: Auth__JwtSecret, Auth__TokenHoras, ...).</summary>
public class AuthOptions
{
    /// <summary>Clave de firma del JWT (mínimo 32 caracteres). Si falta, se genera una temporal por proceso.</summary>
    public string JwtSecret { get; set; } = string.Empty;

    /// <summary>Duración del token en horas.</summary>
    public int TokenHoras { get; set; } = 8;
}

/// <summary>Hash de contraseñas y emisión de tokens JWT.</summary>
public class AuthService
{
    public const string ClaimNombre = "nombre";

    private readonly PasswordHasher<Usuario> _hasher = new();
    private readonly SymmetricSecurityKey _key;
    private readonly TimeSpan _duracion;

    // Hash válido de una contraseña cualquiera: se verifica aunque el usuario no exista,
    // para que el tiempo de respuesta no revele qué usuarios existen.
    private readonly string _hashFalso;

    public AuthService(SymmetricSecurityKey key, TimeSpan duracion)
    {
        _key = key;
        _duracion = duracion;
        _hashFalso = _hasher.HashPassword(new Usuario(), Guid.NewGuid().ToString("N"));
    }

    public SymmetricSecurityKey Key => _key;

    public string HashPassword(Usuario usuario, string password) => _hasher.HashPassword(usuario, password);

    /// <summary>Verifica la contraseña; con usuario nulo igual hace el trabajo y devuelve false.</summary>
    public bool Verificar(Usuario? usuario, string password)
    {
        var resultado = _hasher.VerifyHashedPassword(usuario ?? new Usuario(), usuario?.PasswordHash ?? _hashFalso, password);
        return usuario is { Activo: true } && resultado != PasswordVerificationResult.Failed;
    }

    public (string Token, DateTime Vence) CrearToken(Usuario usuario)
    {
        var vence = DateTime.UtcNow.Add(_duracion);
        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim("name", usuario.Login),
                new Claim(ClaimNombre, usuario.Nombre),
                new Claim("sub", usuario.Id.ToString())
            },
            expires: vence,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), vence);
    }

    /// <summary>Arma la clave a partir de la configuración (o una temporal si no hay).</summary>
    public static SymmetricSecurityKey CrearClave(string? secreto, out bool temporal)
    {
        temporal = string.IsNullOrWhiteSpace(secreto);
        if (temporal)
            secreto = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        if (secreto!.Length < 32)
            throw new InvalidOperationException("Auth:JwtSecret debe tener al menos 32 caracteres");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secreto));
    }
}
