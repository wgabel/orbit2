// Schedules gain on the audio clock, independently of UI timers.
// Continuous voices fade in on start and fade out on explicit stop.
export class AudioEnvelope {
    scheduleAttack(gain, startTime, volume, durationSeconds = 0.02) {
        gain.cancelScheduledValues(startTime);
        gain.setValueAtTime(0, startTime);
        gain.linearRampToValueAtTime(volume, startTime + durationSeconds);
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
