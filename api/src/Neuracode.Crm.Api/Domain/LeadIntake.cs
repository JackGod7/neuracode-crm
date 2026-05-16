using Neuracode.Crm.Api.Data;
using Neuracode.Crm.Api.Data.Entities;

namespace Neuracode.Crm.Api.Domain;

public static class LeadIntake
{
    public static (Contact contact, Activity activity) Build(LeadDraft draft, string activityNote)
    {
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var contact = new Contact
        {
            Id = Guid.NewGuid().ToString(),
            Name = draft.Name,
            Email = draft.Email,
            Phone = draft.Phone,
            Company = draft.Company,
            Source = draft.Source,
            Temperature = draft.Temperature,
            Score = draft.Score,
            Notes = draft.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        var activity = new Activity
        {
            Id = Guid.NewGuid().ToString(),
            Type = "note",
            Description = activityNote,
            ContactId = contact.Id,
            CreatedAt = now
        };

        return (contact, activity);
    }

    public static async Task<Contact> IngestAsync(AppDbContext db, LeadDraft draft, string activityNote)
    {
        var (contact, activity) = Build(draft, activityNote);
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Contacts.Add(contact);
        db.Activities.Add(activity);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return contact;
    }
}
