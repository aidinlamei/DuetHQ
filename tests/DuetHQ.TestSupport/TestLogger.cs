using Serilog;

namespace DuetHQ.TestSupport;

public static class TestLogger
{
    /// <summary>A Serilog logger at Verbose level that writes only to the returned <see cref="InMemoryLogSink"/>.</summary>
    public static (ILogger Logger, InMemoryLogSink Sink) Create()
    {
        var sink = new InMemoryLogSink();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

        return (logger, sink);
    }
}
