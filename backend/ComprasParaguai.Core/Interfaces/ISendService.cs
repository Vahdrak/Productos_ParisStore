namespace ComprasParaguai.Core.Interfaces;

/// <summary>
/// Resultado de un envío de productos.
/// </summary>
public record SendResult(
    int Sent,          // Enviados exitosamente
    int Failed,        // Fallaron (error de validación o API)
    int Total,         // Total intentado
    List<string> ErrorCodes  // Códigos que fallaron
)
{
    public static SendResult Empty => new(0, 0, 0, new());
}

/// <summary>
/// Servicio de envío a un destino (ver SendOptions.Target): batcha, envía, actualiza la
/// columna de estado de ese destino.
/// </summary>
public interface ISendService
{
    /// <summary>Envía TODOS los productos (envío inicial completo).</summary>
    Task<SendResult> SendAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Envía solo los productos Pendiente del destino.</summary>
    Task<SendResult> SendPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Envía N productos al azar (prueba real contra la API).</summary>
    Task<SendResult> SendSampleAsync(int count, CancellationToken cancellationToken = default);
}
