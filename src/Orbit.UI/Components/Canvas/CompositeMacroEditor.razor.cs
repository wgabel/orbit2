using System.Globalization;
using Microsoft.AspNetCore.Components;
using Orbit.Application.Graphs.Commands;
using Orbit.Domain.Graphs;
using Orbit.Domain.Nodes.Composites;
using Orbit.Domain.Parameters;
using Orbit.Presentation.Canvas;

namespace Orbit.UI.Components.Canvas;

/// <summary>Transient form state. Domain validation and mapping math live in CompositeMacroDraft.</summary>
public partial class CompositeMacroEditor
{
    [Parameter, EditorRequired] public CompositeMacroDraft Draft { get; set; } = default!;
    [Parameter] public long CurrentRevision { get; set; }
    [Parameter, EditorRequired] public Func<CompositeMacroDraft, ReplaceCompositeMacrosCommand, string?> Dispatch { get; set; } = default!;
    [Parameter] public EventCallback OnCancel { get; set; }

    private sealed class MacroRow(Guid id, string name, string defaultValue, string baseValue)
    {
        public Guid Id { get; } = id;
        public string Name { get; set; } = name;
        public string Default { get; set; } = defaultValue;
        public string Base { get; set; } = baseValue;
        public List<BindingRow> Bindings { get; } = [];
    }

    private sealed class BindingRow(ParameterAddress address, string atZero, string atOne, string exponent)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public ParameterAddress Address { get; set; } = address;
        public string AtZero { get; set; } = atZero;
        public string AtOne { get; set; } = atOne;
        public string Exponent { get; set; } = exponent;
    }

    private List<MacroRow> Macros { get; } = [];
    private Guid? SelectedId { get; set; }
    private MacroRow? Selected => Macros.SingleOrDefault(macro => macro.Id == SelectedId);
    private bool Stale => CurrentRevision != Draft.Revision;
    private string? Error { get; set; }
    private ReplaceCompositeMacrosCommand? Prepared { get; set; }
    private IReadOnlyList<CompositeMacroDraft.PreviewRow> Previews { get; set; } = [];

    protected override void OnInitialized()
    {
        foreach (var original in Draft.OriginalMacros)
        {
            var row = new MacroRow(original.Id, original.Name, Format(original.DefaultValue), Format(original.BaseValue));
            row.Bindings.AddRange(original.Mapping.Bindings.Select(binding =>
                new BindingRow(binding.Target, Format(binding.AtZero), Format(binding.AtOne), Format(binding.Exponent))));
            Macros.Add(row);
        }
        SelectedId = Macros.FirstOrDefault()?.Id;
        Refresh();
    }

    private void SelectMacro(ChangeEventArgs args) =>
        SelectedId = Guid.TryParse(args.Value?.ToString(), out var id) ? id : null;

    private void AddMacro()
    {
        if (Stale || Macros.Count >= 64) return;
        var names = Macros.Select(macro => macro.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var index = 1;
        while (names.Contains($"Macro {index}")) index++;
        var row = new MacroRow(Guid.NewGuid(), $"Macro {index}", "0.5", "0.5");
        Macros.Add(row);
        SelectedId = row.Id;
        AddBinding(row);
    }

    private void RemoveMacro(MacroRow row)
    {
        Macros.Remove(row);
        SelectedId = Macros.FirstOrDefault()?.Id;
        Refresh();
    }

    private IEnumerable<CompositeMacroDraft.Target> AvailableTargets(BindingRow? current)
    {
        var reserved = Macros.SelectMany(macro => macro.Bindings)
            .Where(binding => !ReferenceEquals(binding, current)).Select(binding => binding.Address).ToHashSet();
        return Draft.Targets.Where(target => !reserved.Contains(target.Address));
    }

    private void AddBinding(MacroRow row)
    {
        if (AvailableTargets(null).FirstOrDefault() is { } target)
            row.Bindings.Add(new BindingRow(target.Address, Format(target.ManualBase), Format(target.ManualBase), "1"));
        Refresh();
    }

    private void RemoveBinding(MacroRow macro, BindingRow binding)
    {
        macro.Bindings.Remove(binding);
        Refresh();
    }

    private void ChangeTarget(BindingRow binding, ChangeEventArgs args)
    {
        var target = AvailableTargets(binding).FirstOrDefault(option => TargetKey(option.Address) == args.Value?.ToString());
        if (target is null || target.Address == binding.Address) return;
        binding.Address = target.Address;
        binding.AtZero = binding.AtOne = Format(target.ManualBase);
        binding.Exponent = "1";
        Refresh();
    }

    private void Refresh()
    {
        Prepared = null;
        Previews = [];
        Error = null;
        try
        {
            var proposed = Macros.Select(macro => new CompositeMacroDefinition(macro.Id, macro.Name,
                Parse(macro.Default), Parse(macro.Base),
                new CompositeMacroMapping(macro.Bindings.Select(binding => new CompositeMacroBinding(binding.Address,
                    Parse(binding.AtZero), Parse(binding.AtOne), Parse(binding.Exponent)))))).ToArray();
            Previews = Draft.Preview(proposed);
            Prepared = Draft.CreateCommand(proposed);
        }
        catch (GraphValidationException exception) { Error = exception.Message; }
    }

    private void EditDraft(ChangeEventArgs args, Action<string> assign)
    {
        assign(args.Value?.ToString() ?? "");
        Refresh();
    }

    private Task CancelAsync() => OnCancel.InvokeAsync();

    private void Apply()
    {
        if (!Stale && Prepared is { } command) Error = Dispatch(Draft, command);
    }

    private static string TargetKey(ParameterAddress address) => $"{address.NodeId:N}:{address.ParameterId:N}";
    private string TargetLabel(CompositeMacroDraft.Target target) =>
        $"{target.NodeName} · {target.Definition.Name} ({target.Address.NodeId.ToString("N")[..8]}){(target.IsActive ? "" : " — dormant")}";
    private static string Format(double value) => value.ToString("G", CultureInfo.InvariantCulture);
    private static double Parse(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed)
            ? parsed : throw new GraphValidationException("Enter finite numeric values.");
}
