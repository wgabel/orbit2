using System.Collections.ObjectModel;
using Orbit.Application.Models;

namespace Orbit.Audio.Compilation;

/// <summary>Channel zero is always the pre-envelope master. Other channels are host-only.</summary>
public sealed class SignalTapManifest
{
    public const int MaxAuxiliaryChannels = 8;
    public static SignalTapManifest Empty { get; } = new(new Dictionary<SignalObservationAddress, int>(), 1);
    internal SignalTapManifest(Dictionary<SignalObservationAddress, int> channels, int channelCount)
    {
        Channels = new ReadOnlyDictionary<SignalObservationAddress, int>(channels);
        ChannelCount = channelCount;
    }
    public IReadOnlyDictionary<SignalObservationAddress, int> Channels { get; }
    public int ChannelCount { get; }
}
