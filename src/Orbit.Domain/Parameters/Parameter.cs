namespace Orbit.Domain.Parameters;

/// <summary>
/// Stores a node's editable base value and optional modulation socket.
/// Effective, sample-varying values are never stored here or sent as UI events.
/// </summary>
public sealed class Parameter
{
    public Parameter(Guid id, ParameterDefinition definition, Guid? modulationPortId = null,
        double? baseValue = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (id == Guid.Empty)
            throw new ArgumentException("Parameter ID cannot be empty.", nameof(id));

        if (string.IsNullOrWhiteSpace(definition.Key) ||
            string.IsNullOrWhiteSpace(definition.Name) ||
            !double.IsFinite(definition.Minimum) ||
            !double.IsFinite(definition.Maximum) ||
            definition.Minimum > definition.Maximum ||
            !definition.IsValidBaseValue(definition.DefaultValue) ||
            !Enum.IsDefined(definition.Unit) ||
            !Enum.IsDefined(definition.Scale))
        {
            throw new ArgumentException("Invalid parameter definition.", nameof(definition));
        }

        if (definition.RequiresRecompile && definition.CanModulate)
            throw new ArgumentException("A structural parameter cannot be modulated.", nameof(definition));

        if (definition.DisplayScale is { } display &&
            (definition.Choices is not null || display.Anchors[0].Value != definition.Minimum ||
             display.Anchors[^1].Value != definition.Maximum))
            throw new ArgumentException("Display scale must span the physical bounds of a continuous parameter.", nameof(definition));

        if (definition.Choices is { } choices &&
            (definition.CanModulate || choices.Count == 0 ||
             choices.Any(choice => !double.IsFinite(choice.Value) || choice.Value < definition.Minimum ||
                 choice.Value > definition.Maximum || string.IsNullOrWhiteSpace(choice.Label)) ||
             choices.Select(choice => choice.Value).Distinct().Count() != choices.Count))
            throw new ArgumentException("Invalid discrete parameter choices.", nameof(definition));

        if (definition.CanModulate != modulationPortId.HasValue ||
            modulationPortId == Guid.Empty)
        {
            throw new ArgumentException(
                "A modulatable parameter needs a non-empty modulation port ID.",
                nameof(modulationPortId));
        }

        Id = id;
        Definition = definition;
        ModulationPortId = modulationPortId;
        SetBaseValue(baseValue ?? definition.DefaultValue);
    }

    public Guid Id { get; }
    public ParameterDefinition Definition { get; }
    public double BaseValue { get; private set; }
    public Guid? ModulationPortId { get; }

    // Graph operations own all parameter edits; callers cannot bypass validation.
    internal void SetBaseValue(double value)
    {
        ValidateBaseValue(value);
        BaseValue = value;
    }

    internal void ValidateBaseValue(double value)
    {
        if (!Definition.IsValidBaseValue(value))
            throw new ArgumentOutOfRangeException(nameof(value), value,
                Definition.Choices is null
                    ? $"Value must be finite and between {Definition.Minimum} and {Definition.Maximum}."
                    : "Value must be one of the defined choices.");
    }
}
