using FluentAssertions;
using Koot.Api.Models;
using Koot.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KootTests;

/// <summary>
/// Tests for the pure in-memory parts of <see cref="GameStateService"/> —
/// session creation, connection tracking, answer recording, disconnect.
/// The timer / broadcast methods require SignalR and are integration-level;
/// they are covered by the hub tests instead.
/// </summary>
public class GameStateServiceTests
{
    // GameStateService requires IHubContext<GameHub>, IServiceScopeFactory, ILogger.
    // We only test the parts that don't call those dependencies directly.
    // We create a minimal instance using Moq.

    private static GameStateService CreateService()
    {
        var hub = new Moq.Mock<Microsoft.AspNetCore.SignalR.IHubContext<Koot.Api.Hubs.GameHub>>();
        var scopeFactory = new Moq.Mock<IServiceScopeFactory>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<GameStateService>.Instance;
        return new GameStateService(hub.Object, scopeFactory.Object, logger);
    }

    private static ActiveSession AddSession(GameStateService svc, string code = "ABC123", int hostId = 1)
    {
        var dbSession = new GameSession
        {
            Id = 1,
            Code = code,
            HostUserId = hostId,
            Status = GameStatus.Lobby,
        };
        var questions = new List<ActiveQuestionInfo>();
        return svc.CreateSession(dbSession, questions);
    }

    // ─── Session lifecycle ────────────────────────────────────────────────────

    [Fact]
    public void CreateSession_CanBeRetrievedByCode()
    {
        var svc = CreateService();
        AddSession(svc, "XYZ999");

        var retrieved = svc.GetSession("XYZ999");
        retrieved.Should().NotBeNull();
        retrieved!.Code.Should().Be("XYZ999");
        retrieved.HostUserId.Should().Be(1);
    }

    [Fact]
    public void GetSession_ReturnsNull_WhenCodeNotFound()
    {
        var svc = CreateService();
        svc.GetSession("DOESNT").Should().BeNull();
    }

    [Fact]
    public void RemoveSession_RemovesIt()
    {
        var svc = CreateService();
        AddSession(svc, "REMOVE");
        svc.RemoveSession("REMOVE");
        svc.GetSession("REMOVE").Should().BeNull();
    }

    // ─── Connection tracking ──────────────────────────────────────────────────

    [Fact]
    public void TrackPlayerConnection_MapsConnectionToParticipant()
    {
        var svc = CreateService();
        AddSession(svc, "TRACK1");

        svc.TrackPlayerConnection("TRACK1", "conn-1", participantId: 10);

        var session = svc.GetSession("TRACK1")!;
        session.ConnectionToParticipant["conn-1"].Should().Be(10);
        session.ParticipantToConnection[10].Should().Be("conn-1");
    }

    [Fact]
    public void TrackPlayerConnection_RemovesFromDisconnected_IfReconnecting()
    {
        var svc = CreateService();
        var session = AddSession(svc, "RECONNECT");
        session.DisconnectedParticipants.Add(99);

        svc.TrackPlayerConnection("RECONNECT", "conn-new", 99);

        session.DisconnectedParticipants.Should().NotContain(99);
    }

    [Fact]
    public void HandleDisconnect_ReturnsCodeAndParticipantId()
    {
        var svc = CreateService();
        AddSession(svc, "DISC1");
        svc.TrackPlayerConnection("DISC1", "conn-disc", 55);

        var result = svc.HandleDisconnect("conn-disc");

        result.Should().NotBeNull();
        result!.Value.code.Should().Be("DISC1");
        result.Value.participantId.Should().Be(55);
    }

    [Fact]
    public void HandleDisconnect_ReturnsNull_ForUnknownConnection()
    {
        var svc = CreateService();
        AddSession(svc, "DISC2");

        var result = svc.HandleDisconnect("unknown-conn");

        result.Should().BeNull();
    }

    [Fact]
    public void HandleDisconnect_MarksParticipantAsDisconnected()
    {
        var svc = CreateService();
        var session = AddSession(svc, "DISC3");
        svc.TrackPlayerConnection("DISC3", "c1", 77);

        svc.HandleDisconnect("c1");

        session.DisconnectedParticipants.Should().Contain(77);
    }

    [Fact]
    public void GetParticipantIdByConnection_ReturnsId_WhenTracked()
    {
        var svc = CreateService();
        AddSession(svc, "GETID");
        svc.TrackPlayerConnection("GETID", "conn-x", 42);

        var id = svc.GetParticipantIdByConnection("conn-x");

        id.Should().Be(42);
    }

    [Fact]
    public void GetParticipantIdByConnection_ReturnsNull_WhenNotTracked()
    {
        var svc = CreateService();
        svc.GetParticipantIdByConnection("not-a-conn").Should().BeNull();
    }

    // ─── Answer tracking ──────────────────────────────────────────────────────

    [Fact]
    public void RecordAnswer_ReturnsFalse_WhenNotAllAnswered()
    {
        var svc = CreateService();
        var session = AddSession(svc, "ANS1");
        // Two active players
        svc.TrackPlayerConnection("ANS1", "c1", 1);
        svc.TrackPlayerConnection("ANS1", "c2", 2);

        // Only one answered
        var allDone = svc.RecordAnswer("ANS1", 1);

        allDone.Should().BeFalse();
    }

    [Fact]
    public void RecordAnswer_ReturnsTrue_WhenAllActivePlayersAnswered()
    {
        var svc = CreateService();
        var session = AddSession(svc, "ANS2");
        svc.TrackPlayerConnection("ANS2", "c1", 1);
        svc.TrackPlayerConnection("ANS2", "c2", 2);

        svc.RecordAnswer("ANS2", 1);
        var allDone = svc.RecordAnswer("ANS2", 2);

        allDone.Should().BeTrue();
    }

    [Fact]
    public void RecordAnswer_ReturnsFalse_WhenNoActivePlayers()
    {
        var svc = CreateService();
        AddSession(svc, "ANS3");
        // No players tracked

        var allDone = svc.RecordAnswer("ANS3", 1);

        allDone.Should().BeFalse(); // 0 >= 0 is true but ActivePlayerCount == 0 guard
    }

    [Fact]
    public void RecordAnswer_ExcludesDisconnectedPlayers_FromCount()
    {
        var svc = CreateService();
        var session = AddSession(svc, "ANS4");
        svc.TrackPlayerConnection("ANS4", "c1", 1);
        svc.TrackPlayerConnection("ANS4", "c2", 2);
        // Disconnect player 2
        svc.HandleDisconnect("c2");

        // Only player 1 is active; they answer → should return true
        var allDone = svc.RecordAnswer("ANS4", 1);

        allDone.Should().BeTrue();
    }
}
