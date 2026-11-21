// Common canvas preparation for both time and frequency domain plots. All
// colors and font values come from the active design-token theme.
export function prepareSignalCanvas(canvas, root) {
    const ratio = Math.min(2, window.devicePixelRatio || 1);
    const width = Math.max(1, Math.round(canvas.clientWidth * ratio));
    const height = Math.max(1, Math.round(canvas.clientHeight * ratio));
    if (canvas.width !== width) canvas.width = width;
    if (canvas.height !== height) canvas.height = height;
    const style = getComputedStyle(root);
    const token = name => style.getPropertyValue(name).trim();
    const ctx = canvas.getContext("2d");
    const colors = {
        background: token("--semantic-color-canvas"), grid: token("--semantic-color-border"),
        trace: token("--semantic-color-modulation"), text: token("--semantic-color-muted")
    };
    ctx.fillStyle = colors.background;
    ctx.fillRect(0, 0, width, height);
    ctx.lineWidth = ratio;
    return { ctx, width, height, ratio, colors,
        fontSize: parseFloat(getComputedStyle(root.querySelector("[data-scale]")).fontSize) * ratio,
        fontFamily: token("--semantic-typography-family") };
}
