namespace Pinball_Aegis.Domain.Entities;

// Represents a flipper machine in the operator fleet inventory
public sealed class MachineItem
{
    public Guid MachineId { get; set; }
    public string MachineName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public int MachineStatus { get; set; }
    public string? MachineNotes { get; set; }
    public DateTime RegisteredAtUtc { get; set; }
    public DateTime? LastUpdated { get; set; }

    public string StatusText => MachineStatus switch
    {
        1 => "Active",
        2 => "Révoquée",
        3 => "Inactive",
        4 => "Maintenance",
        5 => "En attente d'enrôlement",
        _ => "Inconnu"
    };

    public string StatusColor => MachineStatus switch
    {
        1 => "#51CF66", // Green
        2 => "#FF6B6B", // Red
        3 => "#868E96", // Gray
        4 => "#FCC419", // Yellow
        5 => "#FF922B", // Orange
        _ => "#868E96"
    };

    public bool IsEnrolled => MachineStatus == 1;
}
