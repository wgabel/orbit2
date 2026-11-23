namespace Orbit.Audio.Compilation;

/// <summary>Faust Physical Modeling Toolkit wrappers for additional acoustic voices.</summary>
public static class FaustAdditionalPhysicalInstruments
{
    public static string Violin(string pitch, string gate, string expression, string pressure, string position,
        string attack, string release, string vibratoDepth, string vibratoRate, string gain) => $$"""
        (0.65 * ma.tanh(raw) * env * expr * ({{gain}})) with {
            held = ({{gate}})>0.5;
            expr = max(0.0,min(1.0,{{expression}}));
            env = state ~ _;
            state(prev) = select2(held,max(0.0,prev-1.0/(max(0.02,{{release}})*ma.SR)),min(1.0,prev+1.0/(max(0.015,{{attack}})*ma.SR)));
            trackedSemitones = pitchState ~ _;
            target = max(-21.0,min(15.0,{{pitch}}));
            pitchState(prev) = select2(held & held',target,prev+(target-prev)*(1.0-exp(-1.0/(0.005*ma.SR))));
            f = 440.0*pow(2.0,max(-21.0,min(15.0,trackedSemitones+os.osc({{vibratoRate}})*({{vibratoDepth}})*0.01))/12.0);
            bowSpeed = env*(0.035+0.12*expr);
            bowPress = max(0.1,min(0.9,{{pressure}}));
            bowPos = max(0.1,min(0.9,{{position}}));
            raw = pm.violinModel(340.0/f,bowPress,bowSpeed,bowPos) : fi.dcblocker;
        }
        """;

    public static string Brass(string pitch, string gate, string expression, string tension, string mute,
        string attack, string release, string vibratoDepth, string vibratoRate, string gain) => $$"""
        (0.65 * ma.tanh(raw) * env * expr * ({{gain}})) with {
            held = ({{gate}})>0.5;
            expr = max(0.0,min(1.0,{{expression}}));
            env = state ~ _;
            state(prev) = select2(held,max(0.0,prev-1.0/(max(0.02,{{release}})*ma.SR)),min(1.0,prev+1.0/(max(0.015,{{attack}})*ma.SR)));
            trackedSemitones = pitchState ~ _;
            target = max(-21.0,min(15.0,{{pitch}}));
            pitchState(prev) = select2(held & held',target,prev+(target-prev)*(1.0-exp(-1.0/(0.005*ma.SR))));
            f = 440.0*pow(2.0,max(-21.0,min(15.0,trackedSemitones+os.osc({{vibratoRate}})*({{vibratoDepth}})*0.01))/12.0);
            air = env*(0.68+0.23*expr)*(1.0+0.004*no.noise);
            raw = pm.brassModel(340.0/f,max(0.25,min(0.75,{{tension}})),max(0.2,min(0.85,{{mute}})),air) : fi.dcblocker;
        }
        """;

    public static string ElectricGuitar(string pitch, string trigger, string strength, string position, string mute, string gain) => $$"""
        (0.75*ma.tanh(raw)*({{gain}})) with {
            event = ({{trigger}})>0;
            first = 1-1';
            f = 440.0*pow(2.0,max(-21.0,min(15.0,({{pitch}}):ba.sAndH(event|first)))/12.0);
            pluckPos = max(0.05,min(0.95,({{position}}):ba.sAndH(event|first)));
            strikeVelocity = max(0.0,min(1.0,({{strength}}):ba.sAndH(event)));
            sustain = max(0.95,min(1.0,{{mute}}));
            decay = 0.35+45.0*(sustain-0.95);
            loss = pow(0.001,1.0/(f*decay));
            ksDamping = max(0.0,min(1.0,(1.0-loss/cos(ma.PI*f/ma.SR))/0.2));
            samples = max(1,int(0.006*ma.SR+0.5));
            u = float(ba.countup(samples,event))/samples;
            burst = no.noise*strikeVelocity*0.08*sin(ma.PI*u)*(u<1.0);
            stringTone = pm.ks(340.0/f+0.05-6.5*340.0/ma.SR,ksDamping,burst);
            electricAttack = pm.elecGuitar(340.0/f,pluckPos,sustain,strikeVelocity,event);
            raw = (6.0*stringTone+0.2*electricAttack) : fi.dcblocker;
        }
        """;
}
