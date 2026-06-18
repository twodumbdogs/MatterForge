namespace MatterForge.Models;

public static class ContactRoles
{
    public const string Primary = "Primary Contact";
    public const string Billing = "Billing Contact";
    public const string GeneralCounsel = "General Counsel";
    public const string BusinessContact = "Business Contact";
    public const string MatterContact = "Matter Contact";
    public const string Witness = "Witness";
    public const string Adjuster = "Adjuster";
    public const string OutsideCounsel = "Outside Counsel";
    public const string Other = "Other";

    public static readonly string[] All =
    [
        Primary,
        Billing,
        GeneralCounsel,
        BusinessContact,
        MatterContact,
        Witness,
        Adjuster,
        OutsideCounsel,
        Other
    ];
}
