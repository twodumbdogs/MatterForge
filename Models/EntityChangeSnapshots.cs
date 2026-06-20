namespace CMIForge.Models;

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
    public Guid? LeadPartnerId { get; set; }
    public bool RequiresTimeApproval { get; set; }
    public int? TimeIncrementMinutes { get; set; }
    public Guid? TimeCodeSetId { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class ContactChangeSnapshot
{
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string MobilePhone { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
