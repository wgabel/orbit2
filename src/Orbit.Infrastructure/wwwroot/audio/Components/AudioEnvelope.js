// Schedules gain on the audio clock, independently of UI timers.
// Fade-in/out lengths are bounded for very short playback durations.
export class AudioEnvelope {
    schedule(gain, startTime, durationSeconds, volume) {
        const attack = Math.min(0.02, durationSeconds / 3);
        const release = Math.min(0.04, durationSeconds / 3);
        const endTime = startTime + durationSeconds;

        gain.cancelScheduledValues(startTime);
        gain.setValueAtTime(0, startTime);
        gain.linearRampToValueAtTime(volume, startTime + attack);
        gain.setValueAtTime(volume, endTime - release);
        gain.linearRampToValueAtTime(0, endTime);

        return endTime;
    }

    // Continuous voices have no predetermined end; only the attack is
    // scheduled. Stop schedules the release on the same audio clock.
    scheduleSustain(gain, startTime, volume) {
        gain.cancelScheduledValues(startTime);
        gain.setValueAtTime(0, startTime);
        gain.linearRampToValueAtTime(volume, startTime + 0.02);
        return Infinity;
    }

    release(gain, time, durationSeconds = 0.04) {
        // Hold the current instantaneous gain when cancelling the envelope.
        // The fallback retains compatibility with older Web Audio engines.
        if (typeof gain.cancelAndHoldAtTime === "function") {
            gain.cancelAndHoldAtTime(time);
        } else {
            const currentValue = gain.value;
            gain.cancelScheduledValues(time);
            gain.setValueAtTime(currentValue, time);
        }

        const endTime = time + durationSeconds;
        gain.linearRampToValueAtTime(0, endTime);
        return endTime;
    }
}
