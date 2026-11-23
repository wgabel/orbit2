namespace Orbit.Audio.Compilation;

/// <summary>Faust Physical Modeling Toolkit wrappers for additional acoustic voices.</summary>
public static class FaustAdditionalPhysicalInstruments
{
    public static string Violin(string pitch, string gate, string expression, string pressure, string position,
        string attack, string release, string vibratoDepth, string vibratoRate, string gain) => $$"""
        (0.65 * raw/sqrt(1.0+raw*raw) * env * expr * ({{gain}})) with {
            held = ({{gate}})>0.5;
            expr = max(0.0,min(1.0,{{expression}}));
            env = state ~ _;
            state(prev) = select2(held,max(0.0,prev-1.0/(max(0.02,{{release}})*ma.SR)),min(1.0,prev+1.0/(max(0.015,{{attack}})*ma.SR)));
            p = pitchState ~ _;
            target = max(-21.0,min(15.0,{{pitch}}));
            pitchState(prev) = select2(held & held',target,prev+(target-prev)*(1.0-exp(-1.0/(0.005*ma.SR))));
            f = 440.0*pow(2.0,max(-21.0,min(15.0,p+os.osc({{vibratoRate}})*({{vibratoDepth}})*0.01))/12.0);
            bowSpeed = env*(0.1+0.6*expr);
            bowPress = max(0.1,min(0.9,{{pressure}}));
            bowPos = max(0.1,min(0.9,{{position}}));
            raw = pm.violinModel(340.0/f,bowPress,bowSpeed,bowPos) : fi.dcblocker;
        }
        """;

    public static string Brass(string pitch, string gate, string expression, string tension, string mute,
        string attack, string release, string vibratoDepth, string vibratoRate, string gain) => $$"""
        (0.65 * raw/sqrt(1.0+raw*raw) * env * expr * ({{gain}})) with {
            held = ({{gate}})>0.5;
            expr = max(0.0,min(1.0,{{expression}}));
            env = state ~ _;
            state(prev) = select2(held,max(0.0,prev-1.0/(max(0.02,{{release}})*ma.SR)),min(1.0,prev+1.0/(max(0.015,{{attack}})*ma.SR)));
            p = pitchState ~ _;
            target = max(-21.0,min(15.0,{{pitch}}));
            pitchState(prev) = select2(held & held',target,prev+(target-prev)*(1.0-exp(-1.0/(0.005*ma.SR))));
            f = 440.0*pow(2.0,max(-21.0,min(15.0,p+os.osc({{vibratoRate}})*({{vibratoDepth}})*0.01))/12.0);
            air = env*(0.45+0.2*expr)*(1.0+0.004*no.noise);
            raw = pm.brassModel(340.0/f,max(0.25,min(0.75,{{tension}})),max(0.2,min(0.85,{{mute}})),air) : fi.dcblocker;
        }
        """;

    public static string ElectricGuitar(string pitch, string trigger, string strength, string position, string mute, string gain) => $$"""
        (0.65*raw/sqrt(1.0+raw*raw)*({{gain}})) with {
            event = ({{trigger}})>0;
            first = 1-1';
            f = 440.0*pow(2.0,max(-21.0,min(15.0,({{pitch}}):ba.sAndH(event|first)))/12.0);
            pos = max(0.05,min(0.95,({{position}}):ba.sAndH(event|first)));
            v = max(0.0,min(1.0,({{strength}}):ba.sAndH(event)));
            count = max(1,int(0.006*ma.SR));
            phase = float(ba.countup(count,event))/count;
            strike = no.noise * 0.12 * v * sin(ma.PI*phase) * (phase<1.0);
            raw = pm.elecGuitarModel(340.0/f,pos,max(0.6,min(1.0,{{mute}})),strike) : fi.dcblocker;
        }
        """;
}
