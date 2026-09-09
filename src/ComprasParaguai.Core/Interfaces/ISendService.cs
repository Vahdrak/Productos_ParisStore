namespace ComprasParaguai.Core.Interfaces;

/// <summary>
/// Resultado del envío de productos a Compras Paraguai.
/// </summary>
public record SendResult(
    int Sent,          // Enviados exitosamente
    int Failed,        // Fallaron (error de validación o API)
    int Total,         // Total intentado
    List<string> ErrorCodes  // Códigos que fallaron
);

/// <summary>
/// Servicio de envío: toma productos Pendiente, los batca (500), reintenta.
/// Actualiza BD con sync_status según resultado.
/// </summary>
public interface ISendService
{
    /// <summary>
    /// Envía TODOS los productos a Compras Paraguai (envío full inicial).
    /// Batches de 500, marca todos como Enviado en BD.
    /// </summary>
    Task<SendResult> SendAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía todos los productos Pendiente a Compras Paraguai.
    /// Batches de 500, reintentos automáticos, actualiza BD.
    /// </summary>
    Task<SendResult> SendPendingAsync(CancellationToken cancellationToken = default);
}
