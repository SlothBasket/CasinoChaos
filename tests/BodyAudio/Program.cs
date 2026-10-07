using System;
using GWYF_CasinoChaos;
int checks=0;
void Check(bool ok,string message) { checks++;if(!ok)throw new Exception(message); }
void Near(float a,float b,string message)=>Check(Math.Abs(a-b)<0.0001f,message);
Near(HearingRules.Strength(false,true,-1),1,"left side impaired");
Near(HearingRules.Strength(false,true,1),0,"left missing good side");
Near(HearingRules.Strength(true,false,1),1,"right mirrored");
Near(HearingRules.Strength(true,false,-1),0,"right missing good side");
Near(HearingRules.Strength(false,true,0),.2f,"front/back cross-midpoint reach");
Near(HearingRules.Strength(false,true,-.7071f),(.7071f+.25f)/1.25f,"diagonal graded");
Near(HearingRules.Strength(false,false,0),1,"both global world impairment");
Near(HearingRules.Strength(true,true,-1),0,"healthy");
HearingRules.Parameters(false,1,out var gain,out var cutoff); Near(gain,.65f,"bad-side gain");Near(cutoff,750,"bad-side cutoff");
HearingRules.Parameters(true,1,out gain,out cutoff);Near(gain,.55f,"both gain");Near(cutoff,550,"both cutoff");
HearingRules.Parameters(false,0,out gain,out cutoff);Near(gain,1,"neutral gain");Near(cutoff,22000,"neutral cutoff");
float previous=1;
for(int i=0;i<=100;i++){HearingRules.Parameters(false,i/100f,out gain,out cutoff);Check(gain<=previous+.00001f && gain>=.6499f && cutoff>=749 && cutoff<=22001,"continuous bounded reduction");previous=gain;}
Near(.65f*HearingTuning.MouthGain,.39f,"ear/mouth gain composes");
Check(!HearingRules.IsWorldEvent("event:/UI/Click"),"exclude UI");Check(!HearingRules.IsWorldEvent("event:/Ambiences/ElevatorMusicLoop"),"exclude spatial music");Check(HearingRules.IsWorldEvent("event:/Items/QuotaGun/Shoot"),"world gun qualifies");
int notifications=0;BodyPartState.Changed+=(id,part,present)=>notifications++;
Check(!BodyPartState.Receive(0,3,1),"reject invalid id");Check(!BodyPartState.Receive(123,64,1),"reject invalid mask");Check(!BodyPartState.Receive(123,63,0),"reject zero revision");
Check(BodyPartState.Receive(123,63,1),"initial state");Check(notifications==6,"initial six events");
Check(BodyPartState.Receive(123,62,2),"remove left");Check(!BodyPartState.Get(123).Has(CustomBodyPart.LeftEar)&&BodyPartState.Get(123).Has(CustomBodyPart.RightEar),"isolated part");Check(notifications==7,"one change event");
Check(!BodyPartState.Receive(123,63,1)&&!BodyPartState.Receive(123,63,2),"stale duplicate rejected");Check(notifications==7,"no duplicate event");
Check(BodyPartState.Receive(456,61,1),"other player independent");BodyPartState.Clear();Check(BodyPartState.Get(123).Has(CustomBodyPart.LeftEar)&&BodyPartState.Get(456).Has(CustomBodyPart.RightEar),"reset restores defaults");Check(notifications==15,"reset restores missing events only");
Console.WriteLine($"PASS {checks} body-state/directional hearing checks");
