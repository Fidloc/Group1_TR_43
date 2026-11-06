using System.Text.Json;
using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Infrastructure;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Tests;

// UC-07: AC-03, AC-04; пов'язані UC-03 (AC-05, AC-08), UC-04 (AC-07, AC-15), UC-10 (AC-13), UC-11 (AC-14)
public class EventManagementTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0);

    private static CreateEventRequest Request(string title = "Похід", int daysFromNow = 7, int organizer = 1) =>
        new(organizer, title, Now.Date.AddDays(daysFromNow), "Ворохта", "Вихідні в горах");

    // Створює одну подію організатора 1 і повертає її разом зі списками
    private static (List<Event> events, List<Participation> participations, Event ev) SeedOneEvent()
    {
        var events = new List<Event>();
        var participations = new List<Participation>();
        EventManagement.TryCreate(events, participations, Request(), Now, out var ev, out _);
        return (events, participations, ev!);
    }

    // ---------- UC-07: створення ----------

    [Fact]
    public void ValidEvent_IsSaved_AndCreatorBecomesOrganizer() // AC-03
    {
        var events = new List<Event>();
        var participations = new List<Participation>();

        var error = EventManagement.TryCreate(events, participations, Request(), Now,
            out var created, out var organizer);

        Assert.Null(error);
        var saved = Assert.Single(events);
        Assert.Same(created, saved);
        Assert.Equal(1, saved.Id);
        Assert.Equal("Похід", saved.Title);
        Assert.Equal(EventStatus.Created, saved.Status);
        Assert.Equal(EventManagement.InviteCodeLength, saved.InviteCode.Length);

        var role = Assert.Single(participations);
        Assert.Same(organizer, role);
        Assert.Equal(1, role.UserId);
        Assert.Equal(saved.Id, role.EventId);
        Assert.Equal(ParticipantRole.Organizer, role.Role);
    }

    [Fact]
    public void EventForToday_IsAllowed()
    {
        var events = new List<Event>();
        var participations = new List<Participation>();

        var error = EventManagement.TryCreate(events, participations, Request(daysFromNow: 0), Now, out _, out _);

        Assert.Null(error);
        Assert.Single(events);
    }

    [Theory] // AC-04, A1
    [InlineData("")]
    [InlineData("   ")]
    public void MissingTitle_IsRejected_AndNothingSaved(string title)
    {
        var events = new List<Event>();
        var participations = new List<Participation>();

        var error = EventManagement.TryCreate(events, participations, Request(title), Now,
            out var created, out var organizer);

        Assert.Equal("missing_title", error);
        Assert.Null(created);
        Assert.Null(organizer);
        Assert.Empty(events);
        Assert.Empty(participations);
    }

    [Theory] // AC-04, A2
    [InlineData(-1)]
    [InlineData(-365)]
    public void PastDate_IsRejected_AndNothingSaved(int daysFromNow)
    {
        var events = new List<Event>();
        var participations = new List<Participation>();

        var error = EventManagement.TryCreate(events, participations, Request(daysFromNow: daysFromNow), Now,
            out _, out _);

        Assert.Equal("invalid_date", error);
        Assert.Empty(events);
        Assert.Empty(participations);
    }

    [Fact] // AC-04, A2: дату не вказано
    public void DefaultDate_IsRejected()
    {
        var events = new List<Event>();
        var participations = new List<Participation>();

        var error = EventManagement.TryCreate(events, participations,
            new CreateEventRequest(1, "Похід", default, "", ""), Now, out _, out _);

        Assert.Equal("invalid_date", error);
        Assert.Empty(events);
    }

    [Fact]
    public void RejectedRequest_DoesNotChangeExistingEvents()
    {
        var (events, participations, first) = SeedOneEvent();

        var error = EventManagement.TryCreate(events, participations, Request(daysFromNow: -1), Now, out _, out _);

        Assert.Equal("invalid_date", error);
        var only = Assert.Single(events);
        Assert.Equal(first.Id, only.Id);
        Assert.Single(participations);
    }

    [Fact]
    public void SecondEvent_GetsNextId_AndOwnInviteCode()
    {
        var (events, participations, first) = SeedOneEvent();

        EventManagement.TryCreate(events, participations, Request("Футбол", organizer: 2), Now,
            out var second, out var secondOrganizer);

        Assert.Equal(2, second!.Id);
        Assert.NotEqual(first.InviteCode, second.InviteCode);
        Assert.Equal(2, secondOrganizer!.Id);
        Assert.Equal(2, secondOrganizer.UserId);
    }

    [Theory] // формат тіла запиту; назва перевіряється раніше за дату (як на діаграмі)
    [InlineData("""{"organizerUserId":1,"date":"2026-12-01"}""", "missing_title")]
    [InlineData("""{"organizerUserId":1,"title":"  ","date":"2026-12-01"}""", "missing_title")]
    [InlineData("""{"organizerUserId":1,"title":""}""", "missing_title")]
    [InlineData("""{"organizerUserId":1,"title":"Похід"}""", "invalid_date")]
    [InlineData("""{"organizerUserId":1,"title":"Похід","date":"abc"}""", "invalid_date")]
    [InlineData("""{"organizerUserId":1,"title":"Похід","date":null}""", "invalid_date")]
    [InlineData("""{"title":"Похід","date":"2026-12-01"}""", "invalid_request")]
    [InlineData("""{"organizerUserId":0,"title":"Похід","date":"2026-12-01"}""", "invalid_request")]
    [InlineData("""{"organizerUserId":1,"title":5,"date":"2026-12-01"}""", "invalid_request")]
    [InlineData("""[1,2,3]""", "invalid_request")]
    public void TryReadCreate_RejectsBadBody(string json, string expectedCode)
    {
        using var doc = JsonDocument.Parse(json);

        var ok = EventManagement.TryReadCreate(doc.RootElement, out var request, out var code);

        Assert.False(ok);
        Assert.Null(request);
        Assert.Equal(expectedCode, code);
    }

    [Fact]
    public void TryReadCreate_AcceptsValidBody_AndTrimsText()
    {
        using var doc = JsonDocument.Parse(
            """{"organizerUserId":3,"title":"  Похід  ","date":"2026-12-01","place":" Ворохта ","description":null}""");

        var ok = EventManagement.TryReadCreate(doc.RootElement, out var request, out var code);

        Assert.True(ok);
        Assert.Null(code);
        Assert.Equal(new CreateEventRequest(3, "Похід", new DateTime(2026, 12, 1), "Ворохта", ""), request);
    }

    // ---------- UC-04: приєднання ----------

    [Fact]
    public void Join_WithInviteCode_AddsParticipant() // AC-07
    {
        var (events, participations, ev) = SeedOneEvent();

        var error = EventManagement.TryJoin(events, participations,
            new JoinEventRequest(2, ev.InviteCode.ToLowerInvariant()), Now, out var joined, out var participation);

        Assert.Null(error);
        Assert.Same(ev, joined);
        Assert.Equal(2, participations.Count);
        Assert.Equal(ParticipantRole.Participant, participation!.Role);
        Assert.Equal(2, participation.UserId);
        Assert.Equal(ev.Id, participation.EventId);
    }

    [Fact]
    public void Join_Twice_IsRejected()
    {
        var (events, participations, ev) = SeedOneEvent();
        EventManagement.TryJoin(events, participations, new JoinEventRequest(2, ev.InviteCode), Now, out _, out _);

        var error = EventManagement.TryJoin(events, participations,
            new JoinEventRequest(2, ev.InviteCode), Now, out _, out _);

        Assert.Equal("already_joined", error);
        Assert.Equal(2, participations.Count);
    }

    [Fact]
    public void Join_UnknownCode_IsRejected()
    {
        var (events, participations, _) = SeedOneEvent();

        var error = EventManagement.TryJoin(events, participations,
            new JoinEventRequest(2, "NOPE1234"), Now, out _, out _);

        Assert.Equal("event_not_found", error);
        Assert.Single(participations);
    }

    [Theory] // AC-15
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public void Join_InactiveEvent_IsRejected(EventStatus status)
    {
        var (events, participations, ev) = SeedOneEvent();
        ev.Status = status;

        var error = EventManagement.TryJoin(events, participations,
            new JoinEventRequest(2, ev.InviteCode), Now, out _, out _);

        Assert.Equal("event_not_active", error);
        Assert.Single(participations);
    }

    // ---------- UC-10 / UC-11: закриття і скасування ----------

    [Theory] // AC-13, AC-14
    [InlineData(EventStatus.Closed)]
    [InlineData(EventStatus.Cancelled)]
    public void Organizer_ChangesStatus(EventStatus target)
    {
        var (events, participations, ev) = SeedOneEvent();

        var error = EventManagement.TryChangeStatus(events, participations, ev.Id, 1, target, out var changed);

        Assert.Null(error);
        Assert.Same(ev, changed);
        Assert.Equal(target, ev.Status);
    }

    [Fact]
    public void Participant_CannotCloseEvent()
    {
        var (events, participations, ev) = SeedOneEvent();
        EventManagement.TryJoin(events, participations, new JoinEventRequest(2, ev.InviteCode), Now, out _, out _);

        var error = EventManagement.TryChangeStatus(events, participations, ev.Id, 2, EventStatus.Closed, out _);

        Assert.Equal("forbidden_not_organizer", error);
        Assert.Equal(EventStatus.Created, ev.Status);
    }

    [Fact]
    public void ClosedEvent_CannotBeCancelled()
    {
        var (events, participations, ev) = SeedOneEvent();
        EventManagement.TryChangeStatus(events, participations, ev.Id, 1, EventStatus.Closed, out _);

        var error = EventManagement.TryChangeStatus(events, participations, ev.Id, 1, EventStatus.Cancelled, out _);

        Assert.Equal("event_not_active", error);
        Assert.Equal(EventStatus.Closed, ev.Status);
    }

    [Fact]
    public void ChangeStatus_UnknownEvent_IsRejected()
    {
        var (events, participations, _) = SeedOneEvent();

        var error = EventManagement.TryChangeStatus(events, participations, 99, 1, EventStatus.Closed, out _);

        Assert.Equal("event_not_found", error);
    }

    // ---------- UC-03: перегляд ----------

    [Fact]
    public void ToDetails_ShowsEventAndParticipants() // AC-05, AC-08
    {
        var (events, participations, ev) = SeedOneEvent();
        EventManagement.TryJoin(events, participations, new JoinEventRequest(2, ev.InviteCode), Now, out _, out _);

        var details = EventManagement.ToDetails(ev, participations);

        Assert.Equal(ev.Id, details.Id);
        Assert.Equal("Похід", details.Title);
        Assert.Equal("Ворохта", details.Place);
        Assert.Equal("Created", details.Status);
        Assert.Equal(1, details.OrganizerUserId);
        Assert.Equal(new[] { 1, 2 }, details.Participants.Select(p => p.UserId));
        Assert.Equal(new[] { "Organizer", "Participant" }, details.Participants.Select(p => p.Role));
    }
}
