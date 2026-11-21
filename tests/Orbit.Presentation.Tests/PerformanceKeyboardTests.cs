using Orbit.Domain.Nodes.Generators;
using Orbit.Domain.Timing;
using Orbit.Presentation.Canvas;
using Xunit;

namespace Orbit.Presentation.Tests;

public sealed class PerformanceKeyboardTests
{
    [Fact]
    public void PhysicalPianoMapsChromaticOctavesAndRespectsPitchBounds()
    {
        Assert.Equal(Enumerable.Range(60, 13), PerformanceKeyboardLayout.Keys.Select(key => PerformanceKeyboardLayout.Note(4, key)));
        Assert.Equal(69, PerformanceKeyboardLayout.Note(4, PerformanceKeyboardLayout.Keys.Single(key => key.Code == "KeyH")));
        Assert.Equal("A2", MusicalPitch.NoteName(45));
        Assert.Equal("C♯4", MusicalPitch.NoteName(61));
        Assert.Equal(4, PerformanceKeyboardLayout.ReferenceOctave(69));
        Assert.False(PerformanceKeyboardLayout.IsAvailable(-2, 69));
        Assert.False(PerformanceKeyboardLayout.IsAvailable(-1, 127));
        Assert.False(NoteSequencerGenerator.IsPlayable(8, 69));
        Assert.True(NoteSequencerGenerator.IsPlayable(9, 69));
        Assert.True(NoteSequencerGenerator.IsPlayable(127, 69));
        Assert.Equal(9, PerformanceKeyboardLayout.ReferenceOctave(127));
        Assert.True(PerformanceKeyboardLayout.IsAvailable(9, 127));
    }
}
