using System.Diagnostics;
using System.Text.Json;
using Orbit.Application.Graphs;
using Orbit.Application.Graphs.Commands;
using Orbit.Application.Graphs.Sessions;
using Orbit.Application.Models;
using Orbit.Application.Persistence;
using Orbit.Audio.Compilation;

internal static class ConnectionAmountBaselineFixtures
{
    // Use the ordinary seed patch and the existing nested graph probe.
    // The exported JSON preserves exact authored IDs for candidate comparisons.
    public static void Write(string directory, IPatchSession nested, Guid child, Guid nestedEdgeId)
    {
        using var root = ModulationDemoPatch.OpenSession(new ModulationDemoSettings(440, 0.5, 100));
        var compiler = FaustGraphCompiler.CreateBuiltIn();
        var serializer = new PatchDocumentSerializer();
        var cases = new List<object>();

        ExportSeries(root, root.GraphId, root.Snapshot.Connections[0].Id, "root-modulation", [-100, 0, 100], false);
        ExportSeries(root, root.GraphId, root.Snapshot.Connections[1].Id, "root-audio", [-1, 0, 1], true);
        ExportSeries(nested, child, nestedEdgeId, "nested-audio", [-1, 0, 1], true);

        File.WriteAllText(Path.Combine(directory, "connection-amount-baseline.json"),
            JsonSerializer.Serialize(new { schemaVersion = 1, fixtureVersion = 1, blockSize = 128,
                frames = 32768, repeats = 3, faustVersion = "0.19.0", cases }));

        void ExportSeries(IPatchSession session, Guid graphId, Guid edgeId, string name, double[] amounts, bool audio)
        {
            var original = session.Snapshot.GetGraph(graphId).Connections.Single(e => e.Id == edgeId);
            try
            {
                foreach (var amount in amounts)
                {
                    session.Execute(new SetConnectionAmountCommand(graphId, edgeId, amount));
                    var snapshot = session.Snapshot;
                    var patch = serializer.Serialize(serializer.Capture(session, 120, snapshot.Id, []));
                    cases.Add(new { name, amount, audio, graphId, edgeId, patch,
                        mono = Build(false), observed = Build(true) });

                    object Build(bool observed)
                    {
                        var auxiliary = observed ? SignalTapManifest.MaxAuxiliaryChannels : 0;
                        _ = compiler.Compile(snapshot, auxiliary, dynamicTaps: observed);
                        var times = new double[3];
                        FaustGraphProgram program = null!;
                        for (var i = 0; i < times.Length; i++)
                        {
                            var start = Stopwatch.GetTimestamp();
                            program = compiler.Compile(snapshot, auxiliary, dynamicTaps: observed);
                            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                        }
                        if (program.RequiresTransport || program.RhythmicInputs.Count != 0)
                            throw new InvalidOperationException("Static coefficient fixture must not require external inputs.");
                        return new { source = program.Source, values = program.InitialControlValues(),
                            channels = program.SignalTaps.ChannelCount, selectorCount = program.SignalTaps.SelectorCount,
                            sourceGenerationMs = times };
                    }
                }
            }
            finally
            {
                session.Execute(new SetConnectionAmountCommand(graphId, edgeId, original.Amount));
            }
        }
    }
}
