using System.Text.Json;
using MatterForge.Models;

namespace MatterForge.Services;

public static class EntityChangeService
{
    public const string ClientEntityType = "Client";
    public const string MatterEntityType = "Matter";

    public static ClientChangeSnapshot ToSnapshot(Client client)
    {
        return new ClientChangeSnapshot
        {
            Name = client.Name,
            Status = client.Status,
            PrimaryContact = client.PrimaryContact,
            Email = client.Email,
            Phone = client.Phone,
            AddressLine1 = client.AddressLine1,
            AddressLine2 = client.AddressLine2,
            City = client.City,
            State = client.State,
            PostalCode = client.PostalCode,
            Country = client.Country,
            Notes = client.Notes
        };
    }

    public static MatterChangeSnapshot ToSnapshot(Matter matter)
    {
        return new MatterChangeSnapshot
        {
            Name = matter.Name,
            ClientId = matter.ClientId,
            PracticeArea = matter.PracticeArea,
            Status = matter.Status,
            OpenedDate = matter.OpenedDate,
            ResponsibleUserId = matter.ResponsibleUserId,
            Notes = matter.Notes
        };
    }

    public static string Summarize<T>(T current, T proposed)
    {
        var changes = GetChangedProperties(current, proposed).ToList();
        return changes.Count == 0
            ? "No field changes were detected."
            : $"Changed {string.Join(", ", changes)}.";
    }

    public static List<string> GetChangedProperties<T>(T current, T proposed)
    {
        var changes = new List<string>();
        if (current is null || proposed is null)
        {
            return changes;
        }

        foreach (var property in typeof(T).GetProperties())
        {
            var currentValue = property.GetValue(current);
            var proposedValue = property.GetValue(proposed);
            if (!Equals(currentValue, proposedValue))
            {
                changes.Add(SplitPropertyName(property.Name));
            }
        }

        return changes;
    }

    public static string Serialize<T>(T snapshot)
    {
        return JsonSerializer.Serialize(snapshot, FormJson.Options);
    }

    public static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, FormJson.Options);
    }

    public static void Apply(Client client, ClientChangeSnapshot proposed)
    {
        client.Name = proposed.Name.Trim();
        client.Status = proposed.Status.Trim();
        client.PrimaryContact = proposed.PrimaryContact.Trim();
        client.Email = proposed.Email.Trim();
        client.Phone = proposed.Phone.Trim();
        client.AddressLine1 = proposed.AddressLine1.Trim();
        client.AddressLine2 = proposed.AddressLine2.Trim();
        client.City = proposed.City.Trim();
        client.State = proposed.State.Trim();
        client.PostalCode = proposed.PostalCode.Trim();
        client.Country = proposed.Country.Trim();
        client.Notes = proposed.Notes.Trim();
        client.UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static void Apply(Matter matter, MatterChangeSnapshot proposed)
    {
        matter.Name = proposed.Name.Trim();
        matter.ClientId = proposed.ClientId;
        matter.PracticeArea = proposed.PracticeArea.Trim();
        matter.Status = proposed.Status.Trim();
        matter.OpenedDate = proposed.OpenedDate;
        matter.ResponsibleUserId = proposed.ResponsibleUserId;
        matter.Notes = proposed.Notes.Trim();
        matter.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string SplitPropertyName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var words = new List<char> { value[0] };
        for (var i = 1; i < value.Length; i++)
        {
            if (char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
            {
                words.Add(' ');
            }

            words.Add(value[i]);
        }

        return new string(words.ToArray());
    }
}
