using EventOrganizer.Api.Contracts;
using EventOrganizer.Api.Infrastructure;
using EventOrganizer.Api.Models;

namespace EventOrganizer.Tests;

// UC-07 «Створити подію»
public class EventManagementTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0);

    private static CreateEventRequest Request(string title = "Похід", int daysFromNow = 7, int organizer = 1) =>
        new(organizer, title, Now.Date.AddDays(daysFromNow), "Ворохта", "Вихідні в горах");

    [Fact] // AC-03
    public void ValidEvent_IsSaved_AndCreatorBecomesOrganizer()
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

    [Fact] // AC-04, A2
    public void PastDate_IsRejected_AndNothingSaved()
    {
        var events = new List<Event>();
        var participations = new List<Participation>();

        var error = EventManagement.TryCreate(events, participations, Request(daysFromNow: -1), Now,
            out _, out _);

        Assert.Equal("invalid_date", error);
        Assert.Empty(events);
        Assert.Empty(participations);
    }

    [Fact] // AC-04, A2
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
        var events = new List<Event>();
        var participations = new List<Participation>();
        EventManagement.TryCreate(events, participations, Request(), Now, out var first, out _);

        var error = EventManagement.TryCreate(events, participations, Request(daysFromNow: -1), Now, out _, out _);

        Assert.Equal("invalid_date", error);
        var only = Assert.Single(events);
        Assert.Equal(first!.Id, only.Id);
        Assert.Single(participations);
    }
}
