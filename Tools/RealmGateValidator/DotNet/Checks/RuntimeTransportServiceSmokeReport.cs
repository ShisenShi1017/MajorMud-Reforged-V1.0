using System.Text.Json;

namespace RealmGate.Server;

public sealed record RuntimeTransportServiceSmokeReport(bool Passed, IReadOnlyList<string> Checks, IReadOnlyList<string> Failures)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static RuntimeTransportServiceSmokeReport Run(string projectRoot)
    {
        var checks = new List<string>();
        var failures = RuntimeClassicPaidCarriageAuthority.ValidateDecodedGraph(projectRoot).ToList();
        Check(RuntimeClassicPaidCarriageAuthority.Routes.Count == 6, "six-decoded-paid-carriage-routes");
        Check(RuntimeClassicPaidCarriageAuthority.Routes.All(route => route.FareCopper > 0 && route.InsufficientFundsMessageId == 2576), "decoded-fare-and-failure-message-bound");
        Check(RuntimeClassicPaidCarriageAuthority.Routes.Select(route => route.RandomTableId).OrderBy(id => id).Distinct().SequenceEqual(new[] { 2926, 2927 }), "decoded-random-outcome-tables-bound");
        Check(failures.Count == 0, "decoded-carriage-graph-exact");
        return new(failures.Count == 0, checks, failures);

        void Check(bool condition, string name)
        {
            checks.Add(name);
            if (!condition && !failures.Contains(name, StringComparer.Ordinal)) failures.Add(name);
        }
    }
}
