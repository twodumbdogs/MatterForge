namespace MatterForge.Models;

public class ClientChangeSnapshot
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PrimaryContact { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class MatterChangeSnapshot
{
    public string Name { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public string PracticeArea { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateOnly? OpenedDate { get; set; }
    public Guid? ResponsibleUserId { get; set; }
    public string Notes { get; set; } = string.Empty;
}
