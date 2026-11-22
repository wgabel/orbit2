// Local research only. Not an Orbit node or a distributed DSP artifact.
import("stdfaust.lib");
// Reference A4. C3..C6 bounded by model's 3 m waveguide.
pitchHz(p) = 440.0 * pow(2.0, max(-21.0,min(15.0,p))/12.0);
// Compensate the library's 0.05 m tuning adjustment and effective
// fixed round-trip delay (6.5 samples). Verify against pinned 1.2.1 only.
length(f) = 340.0/f + 0.05 - 6.5*340.0/ma.SR;
// Fundamental decay: one reflection coefficient times a two-tap
// averaging filter per round trip. Limit to the physically supported range.
loss(f,t60) = pow(0.001,1.0/(f*max(0.3,min(2.0,t60))));
damping(f,t60) = max(0.0,min(1.0,(1.0-loss(f,t60)/cos(ma.PI*f/ma.SR))/0.2));
process(pitch,event,velocity,t60,external) = pm.ks(length(f),damping(f,t60),drive)*0.5
with {
    trigger = event > 0;
    // Before the first event allow external excitation at the initial pitch.
    // Subsequent fallback/legato pitch values do not retune ringing tails.
    first = 1-1';
    f = pitchHz(pitch : ba.sAndH(trigger | first));
    v = max(0.0,min(1.0,velocity : ba.sAndH(trigger)));
    burstSamples = max(1,int(0.006*ma.SR+0.5));
    u = float(ba.countup(burstSamples,trigger))/burstSamples;
    strike = no.noise * v * 0.08 * sin(ma.PI*u) * (u<1.0);
    // Bound continuous excitation relative to the round-trip loss.
    externalDrive = max(-1.0,min(1.0,external))*0.10*(1.0-loss(f,t60));
    drive = strike + externalDrive;
};
