// Reusable Faust sine processor. Faust generates the samples; the application
// supplies the frequency and the browser engine schedules playback volume.
import("stdfaust.lib");

declare name "Sine";
frequency = hslider("frequency", 440, 20, 20000, 1);
process = os.osc(frequency);
