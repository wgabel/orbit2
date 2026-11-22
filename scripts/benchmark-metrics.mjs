import assert from "node:assert/strict";

export function summarize(samples) {
    assert.ok(samples.length && samples.every(x => Number.isFinite(x) && x >= 0), "Invalid timing samples");
    const values = [...samples].sort((a, b) => a - b);
    const middle = Math.floor(values.length / 2);
    const median = values.length % 2 ? values[middle] : (values[middle - 1] + values[middle]) / 2;
    return { count: values.length, min: values[0], median, p95: values[Math.ceil(values.length * .95) - 1], max: values.at(-1), samples: [...samples] };
}

export function throughput(frames, sampleRateHz, wallMs) {
    assert.ok([frames, sampleRateHz, wallMs].every(x => Number.isFinite(x) && x > 0), "Positive duration required");
    const audioSeconds = frames / sampleRateHz;
    const wallSeconds = wallMs / 1000;
    return { audioSeconds, offlineRealtimeFactor: wallSeconds / audioSeconds, throughputTimesRealtime: audioSeconds / wallSeconds, framesPerWallSecond: frames / wallSeconds };
}

export function inspectOutput(output, frames, expectedChannels, peakLimit, activeTapSlots) {
    assert.equal(output.length, expectedChannels, "Unexpected output channel count");
    const channels = output.map((channel, index) => {
        assert.equal(channel.length, frames, "Unexpected frame count");
        let peak = 0, sum = 0;
        for (const value of channel) {
            assert.ok(Number.isFinite(value), "Non-finite output");
            peak = Math.max(peak, Math.abs(value));
            sum += value * value;
        }
        const rms = Math.sqrt(sum / frames);
        if (index === 0) {
            assert.ok(peak <= peakLimit, "Master exceeded fixture peak limit");
            assert.ok(rms > 1e-6, "Silent benchmark master");
        } else if (index <= activeTapSlots) assert.ok(rms > 1e-6, "Selected tap is silent");
        else assert.equal(peak, 0, "Inactive tap is not silent");
        return { peak, rms };
    });
    return channels;
}
