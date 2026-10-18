using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SearchLab.Core.Diagnostics;

public static class Telemetry
{
    public const string ServiceName = "searchlab";

    public static readonly ActivitySource Source = new(ServiceName);

    public static readonly Meter Meter = new(ServiceName);

    public static class Phases
    {
        public const string Analyze = "search.analyze";
        public const string Parse = "search.parse";
        public const string Score = "search.score";
        public const string Hydrate = "search.hydrate";
        public const string Explain = "search.explain";
    }

    public static class Metrics
    {
        public static readonly Histogram<double> PhaseDuration =
            Meter.CreateHistogram<double>("searchlab.search.phase.duration", unit: "ms");

        public static readonly Counter<long> ZeroResultSearches =
            Meter.CreateCounter<long>("searchlab.search.zero_result.count");
    }
}
