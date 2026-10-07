using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using FMOD;
using GWYF_CasinoChaos;
var random=new Random(42);
for(byte mask=0;mask<64;mask++)
{
 var state=new BodyState(mask,1);
 if(!FartRules.CanAttempt(state))throw new Exception("Known state blocked");
 for(int i=0;i<100;i++)
 {
  byte clip=FartRules.ChooseClip(state,random),pitch=FartRules.ChoosePitch(clip,random);
  if(state.Has(CustomBodyPart.Butt)?clip>=6||pitch!=100:clip!=6||pitch<92||pitch>108)throw new Exception("Ownership/selection violation");
 }
}
if(FartRules.CanAttempt(new BodyState(63,0)))throw new Exception("Unknown state accepted");
var repoRoot=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
var mod=Assembly.LoadFile(Path.Combine(repoRoot,"CasinoChaos/bin/Debug/netstandard2.1/GWYF_CasinoChaos.dll"));
if(mod.GetManifestResourceNames().Length!=7)throw new Exception("Expected seven clips");
using var stream=mod.GetManifestResourceStream("CasinoChaos.Farts.freesound_community-dry-puff-39175.wav");
using var memory=new MemoryStream();stream.CopyTo(memory);var bytes=memory.ToArray();
NativeLibrary.SetDllImportResolver(typeof(FMOD.System).Assembly,(n,a,p)=>NativeLibrary.Load(@"C:\Program Files (x86)\Steam\steamapps\common\Gamble With Your Friends\Gamble With Your Friends_Data\Plugins\x86_64\fmodstudio.dll"));
void Check(RESULT result){if(result!=RESULT.OK)throw new Exception(result.ToString());}
Check(Factory.System_Create(out var core));Check(core.setOutput(OUTPUTTYPE.NOSOUND));Check(core.init(32,INITFLAGS.NORMAL,IntPtr.Zero));
try
{
 var info=new CREATESOUNDEXINFO{cbsize=Marshal.SizeOf<CREATESOUNDEXINFO>(),length=(uint)bytes.Length};
 Check(core.createSound(bytes,MODE.OPENMEMORY|MODE.CREATESAMPLE|MODE._3D|MODE.LOOP_OFF,ref info,out var sound));
 Check(core.getMasterChannelGroup(out var group));
 for(int percent=92;percent<=108;percent++)
 {
  Check(core.playSound(sound,group,true,out var channel));Check(channel.setPitch(percent/100f));Check(channel.getPitch(out float actual));
  if(Math.Abs(actual-percent/100f)>1e-5)throw new Exception("Pitch mismatch");
  Check(channel.setPaused(false));Check(core.update());Check(channel.isPlaying(out bool playing));if(!playing)throw new Exception("No active voice");Check(channel.stop());
 }
 Check(sound.release());
}
finally{core.release();}
Console.WriteLine("PASS: all 64 ownership masks, unknown-state rejection, seven embedded resources, dry-puff native FMOD playback and all 17 pitch settings. NOSOUND; live audio untested.");
