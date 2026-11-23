namespace GymTrack.Domain.Entities;

public class OpeningHour
{
    public int Id { get; set; }
    public DayOfWeek Dia { get; set; }
    public bool Abierto { get; set; }
    public TimeOnly? Apertura { get; set; }
    public TimeOnly? Cierre { get; set; }
}

public class SpecialOpeningHour
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public bool Abierto { get; set; }
    public TimeOnly? Apertura { get; set; }
    public TimeOnly? Cierre { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}
