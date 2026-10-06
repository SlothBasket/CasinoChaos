using System;
using System.Runtime.InteropServices;
using FMOD;
using GWYF_CasinoChaos;
string data=@"C:\Program Files (x86)\Steam\steamapps\common\Gamble With Your Friends\Gamble With Your Friends_Data";
NativeLibrary.SetDllImportResolver(typeof(FMOD.System).Assembly,(n,a,p)=>NativeLibrary.Load(data+@"\Plugins\x86_64\fmodstudio.dll"));
void Check(RESULT r){if(r!=RESULT.OK)throw new Exception(r.ToString());}
void Assert(bool ok,string reason){if(!ok)throw new Exception(reason);}
Check(Factory.System_Create(out var core));Check(core.setOutput(OUTPUTTYPE.NOSOUND));Check(core.init(32,INITFLAGS.NORMAL,IntPtr.Zero));
Check(core.createChannelGroup("CasinoChaos test",out var group));Check(group.setVolume(.42f));Check(group.getNumDSPs(out int original));
using(var chain=new FmodDspChain(group,core)){
Check(group.getNumDSPs(out int added));Assert(added==original+3,"three owned DSPs attached");
chain.Set(.35f*.55f,1200,.08f);Check(core.update());
Check(group.getVolume(out float volume));Assert(Math.Abs(volume-.42f)<.00001,"base volume untouched");
bool gain=false,low=false,dist=false;
for(int i=0;i<added;i++){Check(group.getDSP(i,out var dsp));Check(dsp.getType(out var type));
if(type==DSP_TYPE.FADER){Check(dsp.getParameterFloat((int)DSP_FADER.GAIN,out var v));Console.WriteLine("GAIN="+v);gain|=Math.Abs(v-20*Math.Log10(.35*.55))<.01;}
if(type==DSP_TYPE.LOWPASS_SIMPLE){Check(dsp.getParameterFloat((int)DSP_LOWPASS_SIMPLE.CUTOFF,out var v));Console.WriteLine("LP="+v);low|=Math.Abs(v-1200)<1;}
if(type==DSP_TYPE.DISTORTION){Check(dsp.getParameterFloat((int)DSP_DISTORTION.LEVEL,out var v));Console.WriteLine("DIST="+v);dist|=Math.Abs(v-.08)<.0001;}}
Assert(gain&&low&&dist,"composed tuning reached native DSPs");chain.Set(1,22000,0);
for(int i=0;i<3;i++){Check(group.getDSP(i,out var dsp));Check(dsp.getBypass(out bool bypass));Assert(bypass,"neutral layer bypassed");}
}
Check(group.getNumDSPs(out int restored));Assert(restored==original,"owned DSPs fully removed");Check(group.getVolume(out var after));Assert(Math.Abs(after-.42)<.0001,"volume preserved after cleanup");
Check(group.release());Check(core.release());Console.WriteLine("PASS installed FMOD: attachment, gain/lowpass/distortion, neutral bypass, removal, vanilla volume preserved");


