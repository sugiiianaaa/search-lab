using System.Diagnostics;
using SearchLab.Core.Search;

namespace SearchLab.Core.Diagnostics;

public sealed class PhaseRecorder(string backend)
{
    private readonly List<PhaseTiming> _timings = [];

    public IReadOnlyList<PhaseTiming> Timings => _timings;

    public T Measure<T>(string phase, Func<T> work)
    {
        using Activity? activity = Telemetry.Source.StartActivity(phase);
        activity?.SetTag("backend", backend);

        long start = Stopwatch.GetTimestamp();

        try
        {
            return work();
        }
        finally
        {
            Record(phase, Stopwatch.GetElapsedTime(start));
        }
    }

    private void Record(string phase, TimeSpan elapsed)
    {
        _timings.Add(new PhaseTiming(phase, elapsed));

        Telemetry.Metrics.PhaseDuration.Record(
            elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("phase", phase),
            new KeyValuePair<string, object?>("backend", backend));
    }
}
