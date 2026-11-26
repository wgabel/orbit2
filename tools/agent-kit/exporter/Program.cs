using Orbit.AgentKit.Exporter;
using Orbit.Application.Graphs;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: Orbit.AgentKit.Exporter <catalog.json>");
    return 2;
}

try
{
    var json = CatalogExporter.Export(BuiltInNodes.CreateEditorRegistry());
    var path = Path.GetFullPath(args[0]);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, json);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Catalog export failed: {ex.Message}");
    return 1;
}
