using System.Globalization;
using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Api.Infrastructure;

// Дії з подією: UC-07 створення, UC-03 перегляд, UC-04 приєднання, UC-10/11 закриття і скасування.
// Try* змінюють колекції лише після всіх перевірок; викликати під lock(gate).
public static class EventManagement
{
    public const int InviteCodeLength = 8;

    // UC-07: створення

    // Як на діаграмі: спочатку назва (A1), потім дата (A2). Минулу дату перевіряє TryCreate.
    public static bool TryReadCreate(JsonElement body, out CreateEventRequest? request, out string? code)
    {
        request = null;
        code = "invalid_request";

        if (body.ValueKind != JsonValueKind.Object)
            return false;
        if (!TryReadPositiveInt(body, "organizerUserId", out var organizerId))
            return false;
        if (!TryReadOptionalString(body, "title", out var title) ||
            !TryReadOptionalString(body, "place", out var place) ||
            !TryReadOptionalString(body, "description", out var description))
            return false;

        code = "missing_title";
        if (string.IsNullOrWhiteSpace(title))
            return false;

        code = "invalid_date";
        if (!body.TryGetProperty("date", out var dateElement) ||
            dateElement.ValueKind != JsonValueKind.String ||
            !DateTime.TryParse(dateElement.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var date))
            return false;

        request = new CreateEventRequest(organizerId, title.Trim(), date, place.Trim(), description.Trim());
        code = null;
        return true;
    }

    // Сьогоднішня дата дозволена.
    public static bool IsValidDate(DateTime date, DateTime now) =>
        date != default && date.Date >= now.Date;

    // null — успіх, інакше код відмови. Створювач стає Організатором.
    public static string? TryCreate(
        List<Event> events,
        List<Participation> participations,
        CreateEventRequest request,
        DateTime now,
        out Event? created,
        out Participation? organizer)
    {
        created = null;
        organizer = null;

        if (request.OrganizerUserId <= 0)
            return "invalid_request";
        if (string.IsNullOrWhiteSpace(request.Title))
            return "missing_title";
        if (!IsValidDate(request.Date, now))
            return "invalid_date";

        // TODO (UC-02): перевіряти, що користувач існує.

        created = new Event
        {
            Id = NextId(events.Select(e => e.Id)),
            Title = request.Title.Trim(),
            Date = request.Date,
            Place = (request.Place ?? "").Trim(),
            Description = (request.Description ?? "").Trim(),
            InviteCode = NewInviteCode(events),
            Status = EventStatus.Created
        };
        organizer = new Participation
        {
            Id = NextId(participations.Select(p => p.Id)),
            UserId = request.OrganizerUserId,
            EventId = created.Id,
            Role = ParticipantRole.Organizer,
            JoinedAt = now
        };

        events.Add(created);
        participations.Add(organizer);
        return null;
    }

    // UC-04: приєднання

    public static bool TryReadJoin(JsonElement body, out JoinEventRequest? request)
    {
        request = null;
        if (body.ValueKind != JsonValueKind.Object ||
            !TryReadPositiveInt(body, "userId", out var userId) ||
            !TryReadOptionalString(body, "inviteCode", out var inviteCode) ||
            string.IsNullOrWhiteSpace(inviteCode))
            return false;

        request = new JoinEventRequest(userId, inviteCode.Trim());
        return true;
    }

    public static string? TryJoin(
        List<Event> events,
        List<Participation> participations,
        JoinEventRequest request,
        DateTime now,
        out Event? ev,
        out Participation? participation)
    {
        participation = null;
        ev = events.SingleOrDefault(e =>
            string.Equals(e.InviteCode, request.InviteCode.Trim(), StringComparison.OrdinalIgnoreCase));

        if (ev is null)
            return "event_not_found";
        if (ev.Status != EventStatus.Created)
            return "event_not_active";
        var eventId = ev.Id;
        if (participations.Any(p => p.EventId == eventId && p.UserId == request.UserId))
            return "already_joined";

        participation = new Participation
        {
            Id = NextId(participations.Select(p => p.Id)),
            UserId = request.UserId,
            EventId = ev.Id,
            Role = ParticipantRole.Participant,
            JoinedAt = now
        };
        participations.Add(participation);
        return null;
    }

    // UC-10 / UC-11: закриття і скасування

    public static bool TryReadUserId(JsonElement body, out int userId)
    {
        userId = 0;
        return body.ValueKind == JsonValueKind.Object && TryReadPositiveInt(body, "userId", out userId);
    }

    // Лише Організатор і лише зі стану Created.
    public static string? TryChangeStatus(
        List<Event> events,
        List<Participation> participations,
        int eventId,
        int userId,
        EventStatus target,
        out Event? ev)
    {
        ev = null;
        if (target == EventStatus.Created)
            return "invalid_request";

        ev = events.SingleOrDefault(e => e.Id == eventId);
        if (ev is null)
            return "event_not_found";
        if (!IsOrganizer(participations, eventId, userId))
            return "forbidden_not_organizer";
        if (ev.Status != EventStatus.Created)
            return "event_not_active";

        ev.Status = target;
        return null;
    }

    // UC-03: перегляд

    public static EventDetailsDto ToDetails(Event ev, IEnumerable<Participation> participations)
    {
        var members = participations
            .Where(p => p.EventId == ev.Id)
            .OrderBy(p => p.Id)
            .ToArray();
        var organizerId = members.FirstOrDefault(p => p.Role == ParticipantRole.Organizer)?.UserId ?? 0;

        return new EventDetailsDto(
            ev.Id, ev.Title, ev.Date, ev.Place, ev.Description, ev.InviteCode,
            ev.Status.ToString(), organizerId,
            members.Select(ToParticipant).ToArray());
    }

    public static ParticipantDto ToParticipant(Participation p) =>
        new(p.Id, p.UserId, p.Role.ToString(), p.JoinedAt);

    // Допоміжні

    public static bool IsOrganizer(IEnumerable<Participation> participations, int eventId, int userId) =>
        participations.Any(p => p.EventId == eventId && p.UserId == userId && p.Role == ParticipantRole.Organizer);

    private static int NextId(IEnumerable<int> ids) => ids.DefaultIfEmpty(0).Max() + 1;

    private static string NewInviteCode(List<Event> events)
    {
        while (true)
        {
            var code = Guid.NewGuid().ToString("N")[..InviteCodeLength].ToUpperInvariant();
            if (!events.Any(e => string.Equals(e.InviteCode, code, StringComparison.OrdinalIgnoreCase)))
                return code;
        }
    }

    private static bool TryReadPositiveInt(JsonElement body, string name, out int value)
    {
        value = 0;
        return body.TryGetProperty(name, out var element) &&
               element.ValueKind == JsonValueKind.Number &&
               element.TryGetInt32(out value) &&
               value > 0;
    }

    // Відсутнє або null → "", інакше лише рядок.
    private static bool TryReadOptionalString(JsonElement body, string name, out string value)
    {
        value = "";
        if (!body.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
            return true;
        if (element.ValueKind != JsonValueKind.String)
            return false;
        value = element.GetString() ?? "";
        return true;
    }
}
