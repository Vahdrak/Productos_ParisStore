using ComprasParaguai.Core.Interfaces;
using ComprasParaguai.Core.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using System.Runtime.CompilerServices;

namespace ComprasParaguai.Infrastructure.Readers;

/// <summary>
/// Lee SENSEI.PARAGUAY_COMPRAS_VIEW desde Oracle en streaming (DataReader).
/// Implementación de IViewReader. Sin cargar todo en memoria.
/// </summary>
public class OracleViewReader : IViewReader
{
    private readonly string _connectionString;
    private readonly ILogger<OracleViewReader> _logger;

    public OracleViewReader(string connectionString, ILogger<OracleViewReader> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Lee la vista en streaming (DataReader sin buffering de SQL).
    /// Retorna un IAsyncEnumerable que carga fila por fila.
    /// </summary>
    public async IAsyncEnumerable<ProductoView> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string query = @"
            SELECT
                SKU AS Code,
                NOMBRE AS Nombre,
                PRECIO_NORMAL AS PrecioNormal,
                REGIMEN_TURISMO AS RegimenTurismo,
                INVENTARIO AS Inventario,
                VALOR_ATRIBUTO_1 AS ValorAtributo1,
                NOMBRE_ATRIBUTO_1 AS NombreAtributo1,
                CATEGORIAS AS Categoria,
                IMAGENES AS Imagenes
            FROM SENSEI.PARAGUAY_COMPRAS_VIEW
            ORDER BY SKU
        ";

        using (var connection = new OracleConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            _logger.LogInformation("Conectado a Oracle. Iniciando lectura de SENSEI.PARAGUAY_COMPRAS_VIEW");

            using (var reader = await connection.ExecuteReaderAsync(query))
            {
                int rowCount = 0;
                while (reader.Read())
                {
                    rowCount++;
                    if (rowCount % 10000 == 0)
                        _logger.LogInformation("Leídas {RowCount} filas", rowCount);

                    var view = new ProductoView
                    {
                        Code = reader.GetString(0) ?? string.Empty,
                        Nombre = reader.GetString(1) ?? string.Empty,
                        PrecioNormal = reader.IsDBNull(2) ? null : reader.GetDecimal(2),
                        RegimenTurismo = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Inventario = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                        ValorAtributo1 = reader.IsDBNull(5) ? null : reader.GetString(5),
                        NombreAtributo1 = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Categoria = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Imagenes = reader.IsDBNull(8) ? null : reader.GetString(8)
                    };

                    yield return view;
                }

                _logger.LogInformation("Lectura completada. Total de filas: {RowCount}", rowCount);
            }
        }
    }
}
