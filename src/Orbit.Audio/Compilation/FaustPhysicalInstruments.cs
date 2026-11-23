namespace Orbit.Audio.Compilation;

/// <summary>Sample-level model cores shared by graph emitters and direct DSP regression tests.</summary>
public static class FaustPhysicalInstruments
{
    // Calibrated against pinned physmodels.lib 1.2.1 (scripts/research/pluck-wrapper.dsp).
    public static string Pluck(string pitch, string trigger, string strength, string decay, string excitation, string outputGain) => $$"""
        (pm.ks(length, damping, drive) * 0.5 * ({{outputGain}})) with {
            event = ({{trigger}}) > 0;
            first = 1-1';
            f = 440.0 * pow(2.0, max(-21.0,min(15.0,({{pitch}}) : ba.sAndH(event | first)))/12.0);
            v = max(0.0,min(1.0,({{strength}}) : ba.sAndH(event)));
            length = 340.0/f + 0.05 - 6.5*340.0/ma.SR;
            loss = pow(0.001,1.0/(f*max(0.3,min(2.0,{{decay}}))));
            damping = max(0.0,min(1.0,(1.0-loss/cos(ma.PI*f/ma.SR))/0.2));
            burstSamples = max(1,int(0.006*ma.SR+0.5));
            u = float(ba.countup(burstSamples,event))/burstSamples;
            strike = no.noise * v * 0.08 * sin(ma.PI*u) * (u<1.0);
            drive = strike + max(-1.0,min(1.0,{{excitation}}))*0.10*(1.0-loss);
        }
        """;

    public static string Organ(string pitch, string gate, string trigger, string swell, string click, string percussion, IReadOnlyList<string> drawbars)
    {
        if (drawbars.Count != 9) throw new ArgumentException("Nine drawbars required.", nameof(drawbars));
        string[] ratios = ["0.5", "1.5", "1.0", "2.0", "3.0", "4.0", "5.0", "6.0", "8.0"];
        var partials = string.Join(" + ", ratios.Select((ratio,i) => $"tone({ratio}) * drawbar({drawbars[i]})"));
        return $$"""
            ((({{partials}}) * 0.10 + keyNoise + harmonic) * env * ({{swell}})) with {
                f = 440.0 * pow(2.0, max(-36.0,min(24.0,{{pitch}}))/12.0);
                held = ({{gate}}) > 0.5;
                event = (({{trigger}}) > 0) | (held & (1-held'));
                env = (gateStep ~ _);
                gateStep(prev) = select2(held,max(0.0,prev-1.0/(0.012*ma.SR)),min(1.0,prev+1.0/(0.004*ma.SR)));
                // Continuous drawbar taper; position zero is exactly silent.
                floorGain = pow(10.0,-24.0/20.0);
                drawbar(d) = max(0.0,(pow(10.0,(max(0.0,min(8.0,d))-8.0)*3.0/20.0)-floorGain)/(1.0-floorGain)) * (d>0.0);
                tone(r) = os.osc(min(f*r,0.45*ma.SR)) * (f*r < 0.45*ma.SR);
                clickSamples = max(1,int(ma.SR*0.007));
                u = float(ba.countup(clickSamples,event))/clickSamples;
                keyNoise = (no.noise : fi.highpass(1,700) : fi.lowpass(1,4500)) * sin(ma.PI*u) * (u<1.0) * 0.035 * ({{click}});
                percussionSamples = max(1,int(ma.SR*0.35));
                t = float(ba.countup(percussionSamples,event))/percussionSamples;
                harmonic = tone(3.0) * exp(-6.908*t) * (t<1.0) * 0.07 * ({{percussion}});
            }
            """;
    }

    // Upstream finite-element modal bar data, driven by a simple mallet impulse.
    public static string Marimba(string pitch, string trigger, string strength, string hardness, string position, string decay, string gain) => $$"""
        (strike : pm.marimbaBarModel(f,pos,t60,1.0,5.0)) * ({{gain}}) with {
            event = ({{trigger}})>0;
            first = 1-1';
            f = 440.0*pow(2.0,max(-21.0,min(15.0,({{pitch}}):ba.sAndH(event|first)))/12.0);
            pos = int(max(0.0,min(4.0,({{position}}):ba.sAndH(event|first)))+0.5);
            t60 = max(0.15,min(1.5,{{decay}}));
            hard = max(0.0,min(1.0,({{hardness}}):ba.sAndH(event|first)));
            v = max(0.0,min(1.0,{{strength}}));
            strike = (float(event)*v : fi.lowpass(1,700.0+9000.0*hard));
        }
        """;

    public static string Rotary(string input, string speed, string am, string doppler, string mix) => $$"""
        (x*(1.0-wet) + (bassOut+hornOut)*wet) with {
            x = {{input}};
            wet = {{mix}};
            setting = max(0.0,min(2.0,{{speed}}));
            // Complementary crossover: the bands exactly sum to the original signal.
            bass = x : fi.lowpass(1,800);
            horn = x-bass;
            target(slow,fast) = slow*min(1.0,setting)+(fast-slow)*max(0.0,setting-1.0);
            inertia(goal,up,down) = (step ~ _) with {
                step(prev) = prev+(goal-prev)*(1.0-exp(-1.0/(ma.SR*select2(goal>prev,down,up))));
            };
            bassRate = inertia(target(0.72,6.4),2.0,1.17);
            hornRate = inertia(target(0.77,6.9),0.33,0.27);
            bassPhase = 2.0*ma.PI*os.lf_sawpos(bassRate);
            hornPhase = 2.0*ma.PI*(os.lf_sawpos(hornRate)+0.25);
            rotor(band,phase,depth,travel) = (band : de.fdelay(1920,ma.SR*0.001*(2.0+travel*({{doppler}})*sin(phase))))
                * (1.0-depth*({{am}})*(0.5+0.5*cos(phase)));
            bassOut = rotor(bass,bassPhase,0.65,0.9);
            hornOut = rotor(horn,hornPhase,0.95,0.45);
        }
        """;
}
