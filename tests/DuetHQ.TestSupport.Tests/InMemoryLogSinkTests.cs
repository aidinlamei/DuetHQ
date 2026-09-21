using Serilog.Events;

namespace DuetHQ.TestSupport.Tests;

public sealed class InMemoryLogSinkTests
{
    [Fact]
    public void Emit_LogWithProperties_KeepsRawTemplateAndProperties()
    {
        var (logger, sink) = TestLogger.Create();

        logger.Information("Recorded {Count} items for {@Payload}", 3, new { Kind = "probe" });

        var logEvent = sink.Events.ShouldHaveSingleItem();
        logEvent.MessageTemplate.Text.ShouldBe("Recorded {Count} items for {@Payload}");
        logEvent.Properties["Count"].ShouldBeOfType<ScalarValue>().Value.ShouldBe(3);
        var payload = logEvent.Properties["Payload"].ShouldBeOfType<StructureValue>();
        payload.Properties.Single(p => p.Name == "Kind").Value.ShouldBeOfType<ScalarValue>().Value.ShouldBe("probe");
    }

    [Fact]
    public void Events_ReadBeforeMoreEmits_IsASnapshot()
    {
        var (logger, sink) = TestLogger.Create();
        logger.Information("first");

        var snapshot = sink.Events;
        logger.Information("second");

        snapshot.Count.ShouldBe(1);
        sink.Events.Count.ShouldBe(2);
    }

    [Fact]
    public void Emit_ConcurrentWriters_KeepsEveryEvent()
    {
        var (logger, sink) = TestLogger.Create();

        Parallel.For(0, 1_000, i => logger.Information("event {Index}", i));

        sink.Events.Count.ShouldBe(1_000);
    }

    [Fact]
    public void Clear_AfterEvents_EmptiesTheSink()
    {
        var (logger, sink) = TestLogger.Create();
        logger.Information("something");

        sink.Clear();

        sink.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Emit_NullEvent_Throws()
    {
        var sink = new InMemoryLogSink();

        Should.Throw<ArgumentNullException>(() => sink.Emit(null!));
    }

    [Fact]
    public void Create_Verbose_CapturesEveryLevel()
    {
        var (logger, sink) = TestLogger.Create();

        logger.Verbose("v");
        logger.Fatal("f");

        sink.Events.Select(e => e.Level).ShouldBe([LogEventLevel.Verbose, LogEventLevel.Fatal]);
    }
}
