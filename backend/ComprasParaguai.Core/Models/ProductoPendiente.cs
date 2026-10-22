namespace ComprasParaguai.Core.Models;

/// <summary>Producto pendiente de envío, indicando si hoy está retenido (y por qué regla).</summary>
public class ProductoPendiente : ProductoApi
{
    /// <summary>True si una pausa o excepción lo retiene: no se enviará mientras dure.</summary>
    public bool Retenido { get; set; }

    /// <summary>Qué lo retiene: "LACOSTE en INDUMENTARIA", "Excepción: retener SKU", etc.</summary>
    public string? RetenidoPor { get; set; }
}

/// <summary>Filtros de la lista de pendientes de un destino (se aplican en el servidor).</summary>
public class PendientesFiltro
{
    public string? Code { get; set; }

    /// <summary>true = solo con régimen turismo, false = solo sin régimen, null = todos.</summary>
    public bool? Regimen { get; set; }

    /// <summary>"retenidos", "enviables" o null (todos).</summary>
    public string? Retencion { get; set; }


    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>Una página de pendientes más los totales del filtro (no solo de la página).</summary>
public class ProductoPendienteListDto
{
    public List<ProductoPendiente> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }

    /// <summary>De los que cumplen el filtro: cuántos tienen régimen turismo y cuántos están retenidos.</summary>
    public int ConRegimen { get; set; }
    public int Retenidos { get; set; }
}
