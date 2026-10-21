using ComprasParaguai.Core.Models;

namespace ComprasParaguai.Infrastructure.Repositories;

/// <summary>
/// Fragmentos SQL de la regla efectiva de envío (pausas y excepciones). Se interpolan dentro de consultas que
/// tengan el alias <c>p</c> (productos) y <c>c</c> (categorias del producto, LEFT JOIN).
/// El destino es un literal de un switch cerrado: nunca viene de input externo.
/// <para>Regla: excepción activa 'enviar' => se envía; 'retener' => retenido; sin excepción, retenido si ALGUNA
/// pausa activa del destino lo cubre (marca coincide Y categoría dentro de su subárbol, según lo que la pausa fije).</para>
/// </summary>
internal static class EnvioSql
{
    public static string Destino(SyncTarget target) => target switch
    {
        SyncTarget.ComprasParaguai => "compras",
        SyncTarget.ComparaParaguai => "compara",
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
    };

    private static string Excepcion(string t, string accion) =>
        $"EXISTS (SELECT 1 FROM envio_excepciones e WHERE e.producto_id = p.id AND e.target = '{t}' AND e.activo AND e.accion = '{accion}')";

    /// <summary>Alguna pausa activa del destino cubre al producto (opcionalmente solo las que retiran del destino).</summary>
    private static string PausaCubre(string t, bool soloRetirar) => $@"EXISTS (
            SELECT 1 FROM envio_pausas pa
            LEFT JOIN categorias pc ON pc.id = pa.categoria_id
            WHERE pa.target = '{t}' AND pa.activo{(soloRetirar ? " AND pa.retirar_del_destino" : "")}
              AND (pa.marca_id IS NULL OR pa.marca_id = p.marca_id)
              AND (pa.categoria_id IS NULL OR c.ruta = pc.ruta OR left(c.ruta, length(pc.ruta) + 1) = pc.ruta || '>'))";

    /// <summary>Condición booleana: el producto está retenido (no debe enviarse) para el destino.</summary>
    public static string Retenido(SyncTarget target)
    {
        var t = Destino(target);
        return $"(CASE WHEN {Excepcion(t, "enviar")} THEN FALSE WHEN {Excepcion(t, "retener")} THEN TRUE ELSE {PausaCubre(t, false)} END)";
    }

    /// <summary>Condición booleana: una pausa con "retirar del destino" cubre al producto (y no hay excepción 'enviar').</summary>
    public static string Retirable(SyncTarget target)
    {
        var t = Destino(target);
        return $"(NOT {Excepcion(t, "enviar")} AND {PausaCubre(t, true)})";
    }

    /// <summary>Texto de qué lo retiene ("LACOSTE en INDUMENTARIA"), o NULL si no está retenido.</summary>
    public static string RetenidoPor(SyncTarget target)
    {
        var t = Destino(target);
        return $@"(CASE
            WHEN {Excepcion(t, "enviar")} THEN NULL
            WHEN {Excepcion(t, "retener")} THEN 'Excepción: retener SKU'
            ELSE (SELECT string_agg(concat_ws(' en ', pm.nombre, pc.nombre), ', ' ORDER BY pa.id)
                  FROM envio_pausas pa
                  LEFT JOIN marcas pm ON pm.id = pa.marca_id
                  LEFT JOIN categorias pc ON pc.id = pa.categoria_id
                  WHERE pa.target = '{t}' AND pa.activo
                    AND (pa.marca_id IS NULL OR pa.marca_id = p.marca_id)
                    AND (pa.categoria_id IS NULL OR c.ruta = pc.ruta OR left(c.ruta, length(pc.ruta) + 1) = pc.ruta || '>'))
        END)";
    }
}
