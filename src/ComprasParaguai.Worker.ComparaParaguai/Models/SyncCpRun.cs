namespace ComprasParaguai.Worker.ComparaParaguai.Models;

public class SyncCpRun
{
    public long Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? BatchNum { get; set; }
    public int? RowsSent { get; set; }
    public int? HttpStatus { get; set; }
    public string? ResponseText { get; set; }
    public bool? Success { get; set; }
    public string? ErrorMsg { get; set; }
    public string? Notes { get; set; }
}
