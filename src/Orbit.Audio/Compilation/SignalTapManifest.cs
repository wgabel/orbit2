using System.Collections.ObjectModel;
using Orbit.Application.Models;

namespace Orbit.Audio.Compilation;

/// <summary>Channel zero is always the pre-envelope master. Other channels are host-only.</summary>
public sealed class SignalTapManifest
{
    public const int MaxAuxiliaryChannels = 8;
    public static SignalTapManifest Empty { get; } = new(new Dictionary<SignalObservationAddress, int>(), 1);
    internal SignalTapManifest(Dictionary<SignalObservationAddress, int> channels, int channelCount, int selectorCount = 0)
    {
        ObservationIds = new ReadOnlyDictionary<SignalObservationAddress, int>(channels);
        Channels = selectorCount == 0 ? ObservationIds : new ReadOnlyDictionary<SignalObservationAddress, int>(
            channels.Where(pair => pair.Value == 0).ToDictionary());
        ChannelCount = channelCount;
        SelectorCount = selectorCount;
    }
    /// <summary>Fixed channel indices for offline probes; only master aliases in a dynamic program.</summary>
    public IReadOnlyDictionary<SignalObservationAddress, int> Channels { get; }
    /// <summary>Logical signal identities in dynamic programs; static channel indices otherwise (0 is master).</summary>
    public IReadOnlyDictionary<SignalObservationAddress, int> ObservationIds { get; }
    public int ChannelCount { get; }
    public int SelectorCount { get; }
}
