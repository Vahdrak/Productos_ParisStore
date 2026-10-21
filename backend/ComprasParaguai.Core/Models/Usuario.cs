namespace ComprasParaguai.Core.Models;

/// <summary>Usuario del panel (tabla usuarios).</summary>
public class Usuario
{
    public int Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
