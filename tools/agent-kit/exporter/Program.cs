using Orbit.AgentKit.Exporter;
using Orbit.Application.Graphs;

var exportRules = args.Length == 2 && args[0] == "--connection-rules";
var exportPatch = args.Length == 2 && args[0] == "--patch-contract";
if (!exportRules && !exportPatch && (args.Length != 1 || args[0].StartsWith("--", StringComparison.Ordinal)))
{
    Console.Error.WriteLine("Usage: Orbit.AgentKit.Exporter <catalog.json> or --connection-rules <rules.json> or --patch-contract <directory>");
    return 2;
}

try
{
    if (exportPatch)
    {
        var directory = Path.GetFullPath(args[1]);
        var files = PatchContractExporter.Export();
        Directory.CreateDirectory(directory);
        foreach (var (name, content) in files)
            File.WriteAllText(Path.Combine(directory, name), content);
        return 0;
    }

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
