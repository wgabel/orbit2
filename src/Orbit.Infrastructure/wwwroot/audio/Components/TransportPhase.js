// Twelve beats cover whole notes, dotted quarters, triplets and all binary
// divisions without a phase discontinuity. Wrap before writing Float32 audio.
export const TRANSPORT_BEAT_PERIOD = 12;

export function beatAtFrame(anchor, frame, sampleRate) {
    const elapsed = anchor.isPlaying ? Math.max(0, frame - anchor.frame) / sampleRate : 0;
    const beat = anchor.beat + elapsed * anchor.beatsPerMinute / 60;
    return ((beat % TRANSPORT_BEAT_PERIOD) + TRANSPORT_BEAT_PERIOD) % TRANSPORT_BEAT_PERIOD;
}

export function toTransportAnchor(snapshot) {
    if (!Number.isFinite(snapshot?.beatsPerMinute) || snapshot.beatsPerMinute < 20 ||
        snapshot.beatsPerMinute > 300 || !Number.isFinite(snapshot.beatPosition) ||
        snapshot.beatPosition < 0 || typeof snapshot.isPlaying !== "boolean" ||
        (snapshot.isPlaying && (!Number.isSafeInteger(snapshot.clock?.currentSample) ||
            snapshot.clock.currentSample < 0 || !Number.isFinite(snapshot.clock.sampleRate) ||
            snapshot.clock.sampleRate <= 0)))
        throw new Error("Invalid musical transport anchor.");
    return {
        beat: snapshot.beatPosition % TRANSPORT_BEAT_PERIOD,
        frame: snapshot.clock?.currentSample ?? 0,
        sampleRate: snapshot.clock?.sampleRate ?? null,
        beatsPerMinute: snapshot.beatsPerMinute,
        isPlaying: snapshot.isPlaying,
        resetGeneration: snapshot.resetGeneration ?? 0
    };
}
