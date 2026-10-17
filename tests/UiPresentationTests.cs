using System;
using EchoGuard;
internal static class UiPresentationTests {
 static Diagnosis Ready(){var d=new Diagnosis{Status="Ready to Apply Fix",CanEnable=true,Compatible=true,Compatibility="Supported known path"};d.Candidates.Add("synthetic-target");return d;}
 internal static void Run(Action<bool,string> Check){
  var d=Ready();var p=StatusPresentation.From(d,false,false);
  Check(p.Description=="Not checked yet."&&!p.CanFix,"new UI preserves reference initial state before user diagnosis");
  Check(p.Message=="Ready to detect echo source."&&p.Color=="#4F596E","initial status is neutral, not a fabricated success");
  p=StatusPresentation.From(d,true,true);Check(p.Color=="#288AFF"&&!p.CanFix,"analyzing state is blue and blocks Fix Echo");
  p=StatusPresentation.From(d,true,false);Check(p.CanFix&&p.Description=="Supported configuration.","supported completed diagnosis enables secondary action");
  d.Candidates.Clear();Check(!StatusPresentation.From(d,true,false).CanFix,"missing target session cannot enable Fix Echo");
  d=Ready();d.Conflict=true;p=StatusPresentation.From(d,true,false);Check(!p.CanFix&&p.Color=="#DDAA55","helper conflict remains blocked and amber");
  d=new Diagnosis{Status="Protected",ProtectionEnabled=true,DuplicatePath="synthetic exact playback path"};p=StatusPresentation.From(d,false,false);
  Check(p.Description=="Protection active."&&p.Color=="#40C98A","backend-confirmed protection is visible even before manual diagnosis");
  Check(p.Message==d.DuplicatePath&&p.CanFix,"active state preserves target path and reversible disable control");
  Check(!p.Message.Contains("no echo"),"active indicator does not assert audible echo elimination");
  d=new Diagnosis{Status="Waiting for screen share",ProtectionEnabled=true};p=StatusPresentation.From(d,false,false);
  Check(p.CanFix&&p.Color!="#40C98A","armed protection can be disabled without claiming active readback");
  d=new Diagnosis{Status="Unsupported Audio Setup",Compatibility="Unsupported by this preview"};p=StatusPresentation.From(d,true,false);
  Check(!p.CanFix&&p.Description=="Unsupported configuration."&&p.Color=="#DDAA55","unsupported route is explicitly amber and disabled");
  d=new Diagnosis{Status="Fix Requires Attention",Detail="Windows could not complete an audio operation."};p=StatusPresentation.From(d,true,false);
  Check(p.Color=="#EF6B73"&&!p.CanFix,"Windows error is red and does not authorize exclusion");
  d=new Diagnosis{Status="Fix Requires Attention",Detail="External override observed."};p=StatusPresentation.From(d,true,false);
  Check(p.Color=="#DDAA55"&&!p.CanFix,"unconfirmed external override stays attention rather than success");
  d=new Diagnosis{Status="Ready to Diagnose",Selection=new SelectionResult{Confidence="Suggested"}};p=StatusPresentation.From(d,true,false);
  Check(!p.CanFix&&p.Message.Contains("not confirmed"),"suggested microphone remains an estimate on simplified UI");
  d=new Diagnosis{Status="Discord Not Running"};Check(StatusPresentation.From(d,true,false).Message.Contains("Open Discord"),"missing Discord has an actionable honest status");
  d=Ready();p=StatusPresentation.From(d,false,false);Check(!p.CanFix,"background discovery alone never opts in or enables initial fix");
 }
}
