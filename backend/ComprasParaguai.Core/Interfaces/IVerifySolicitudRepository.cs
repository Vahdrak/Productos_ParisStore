namespace ComprasParaguai.Core.Interfaces;

public static class VerifyTipo
{
    /// <summary>Foto de los productos con stock que Compras marca como no disponibles.</summary>
    public const string Disponibilidad = "disponibilidad";
    /// <summary>Muestra al azar de productos Enviados, buscados uno por uno en la API de Compras.</summary>
    public const string Muestra = "muestra";

    public static bool EsValido(string? tipo) => tipo is Disponibilidad or Muestra;
}

public static class VerifyEstado
{
    public const string Pendiente = "pendiente";
    public const string Corriendo = "corriendo";
    public const string Ok = "ok";
    public const string Error = "error";
}

/// <summary>
/// Cola de pedidos para Worker.Verify. La API (con sesión iniciada) solo los crea; el worker los toma y los ejecuta.
/// </summary>
public interface IVerifySolicitudRepository
{
    /// <summary>Crea un pedido. Si ya hay uno abierto del mismo tipo devuelve ese, con <c>Nueva = false</c>.</summary>
    Task<(VerifySolicitudDto Solicitud, bool Nueva)> SolicitarAsync(string tipo, string origen, string? usuario, CancellationToken cancellationToken = default);

    /// <summary>Toma el pedido pendiente más viejo y lo pasa a "corriendo". Null si no hay.</summary>
    Task<VerifySolicitudDto?> ReclamarSiguienteAsync(CancellationToken cancellationToken = default);

    /// <summary>Cierra un pedido: con <paramref name="error"/> queda en "error", si no en "ok".</summary>
    Task TerminarAsync(long id, string? resultado, string? error, CancellationToken cancellationToken = default);

    /// <summary>Los pedidos que quedaron "corriendo" (el worker murió a mitad) pasan a "error". Devuelve cuántos.</summary>
    Task<int> MarcarInterrumpidasAsync(CancellationToken cancellationToken = default);

    /// <summary>Cuándo se pidió por última vez este tipo (de cualquier origen). Null si nunca.</summary>
    Task<DateTime?> UltimaSolicitudAsync(string tipo, CancellationToken cancellationToken = default);

    Task<List<VerifySolicitudDto>> ListarAsync(int limit = 10, CancellationToken cancellationToken = default);
}

public class VerifySolicitudDto
{
    public long Id { get; set; }
    public string Tipo { get; set; } = "";
    public string Estado { get; set; } = "";
    public string Origen { get; set; } = "";
    public string? SolicitadoPor { get; set; }
    public DateTime SolicitadoAt { get; set; }
    public DateTime? IniciadoAt { get; set; }
    public DateTime? TerminadoAt { get; set; }
    public string? Resultado { get; set; }
    public string? Error { get; set; }
}
