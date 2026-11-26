using Orbit.AgentKit.Exporter;
using Orbit.Application.Graphs;

var exportRules = args.Length == 2 && args[0] == "--connection-rules";
if (!exportRules && (args.Length != 1 || args[0].StartsWith("--", StringComparison.Ordinal)))
{
    Console.Error.WriteLine("Usage: Orbit.AgentKit.Exporter <catalog.json> or --connection-rules <rules.json>");
    return 2;
}

try
{
    var json = exportRules ? ConnectionRulesExporter.Export() : CatalogExporter.Export(BuiltInNodes.CreateEditorRegistry());
    var path = Path.GetFullPath(args[^1]);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, json);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Agent Kit export failed: {ex.Message}");
    return 1;
}
