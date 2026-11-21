import { spectrumFrequencyPosition, spectrumTicks } from "./spectrumAnalysis.js";
import { prepareSignalCanvas } from "./signalPlot.js";

const frequencyLabel = frequency => frequency >= 1000 ? `${frequency / 1000}k` : `${frequency}`;

export function drawSpectrum(canvas, root, spectrum) {
    const { ctx, width, height, ratio, colors, fontSize, fontFamily } = prepareSignalCanvas(canvas, root);
    if (!spectrum) return;
    const { decibels, binWidth, peakDecibels } = spectrum;
    const maximum = (decibels.length - 1) * binWidth;
    const minimum = binWidth;
    const topDb = Math.max(0, Math.ceil(peakDecibels / 20) * 20);
    const bottomDb = -100;
    ctx.font = `${fontSize}px ${fontFamily}`;
    ctx.textBaseline = "middle";
    const left = Math.ceil(ctx.measureText("-100").width + fontSize);
    const right = width - fontSize / 2;
    const top = fontSize / 2;
    const bottom = height - fontSize * 2;
    if (right <= left || bottom <= top) return;
    const x = frequency => left + spectrumFrequencyPosition(frequency, minimum, maximum) * (right - left);
    const y = db => bottom - (Math.max(bottomDb, Math.min(topDb, db)) - bottomDb) / (topDb - bottomDb) * (bottom - top);
    ctx.strokeStyle = colors.grid;
    ctx.fillStyle = colors.text;
    ctx.textAlign = "right";
    const dbStep = Math.max(20, Math.ceil((topDb - bottomDb) / Math.max(1, Math.floor((bottom - top) / (fontSize * 1.5))) / 20) * 20);
    for (let db = topDb; db >= bottomDb; db -= dbStep) {
        const row = y(db);
        ctx.beginPath(); ctx.moveTo(left, row); ctx.lineTo(right, row); ctx.stroke();
        ctx.fillText(db === topDb ? `${db} dB` : String(db), left - fontSize / 3, row);
    }
    ctx.textAlign = "center";
    let lastEnd = left - fontSize;
    for (const frequency of spectrumTicks(minimum, maximum)) {
        const column = x(frequency);
        const label = frequencyLabel(frequency);
        const halfWidth = ctx.measureText(label).width / 2;
        if (column - halfWidth < lastEnd + ratio * 2 || column + halfWidth > width) continue;
        ctx.beginPath(); ctx.moveTo(column, top); ctx.lineTo(column, bottom); ctx.stroke();
        ctx.fillText(label, column, bottom + fontSize);
        lastEnd = column + halfWidth;
    }
    ctx.strokeStyle = colors.trace;
    ctx.beginPath();
    // Preserve narrow peaks when many high-frequency bins occupy a pixel.
    let previousPixel = NaN, maximumDb = -Infinity, started = false;
    const flush = () => {
        if (!Number.isFinite(previousPixel)) return;
        if (!started) { ctx.moveTo(previousPixel, y(maximumDb)); started = true; }
        else ctx.lineTo(previousPixel, y(maximumDb));
    };
    for (let bin = 1; bin < decibels.length; bin++) {
        const pixel = Math.round(x(bin * binWidth));
        if (pixel !== previousPixel) {
            flush();
            previousPixel = pixel;
            maximumDb = decibels[bin];
        } else maximumDb = Math.max(maximumDb, decibels[bin]);
    }
    flush();
    ctx.stroke();
}
