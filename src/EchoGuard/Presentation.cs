using System;
namespace EchoGuard {
 internal sealed class StatusPresentation {
  internal string Description,Message,Color; internal bool CanFix;
  internal static StatusPresentation From(Diagnosis d,bool checkedByUser,bool analyzing){
   var p=new StatusPresentation{Description="Not checked yet.",Message="Ready to detect echo source.",Color="#4F596E",CanFix=false};
   if(analyzing){p.Description="Analyzing audio routing...";p.Message="Checking the selected microphone and supported audio paths.";p.Color="#288AFF";return p;}
   if(!checkedByUser&&!d.ProtectionEnabled&&d.Status!="Protected")return p;
   p.CanFix=d.ProtectionEnabled||(checkedByUser&&d.CanEnable&&!d.Conflict&&d.Candidates.Count>0);
   if(d.Status=="Protected"){p.Description="Protection active.";p.Message=d.DuplicatePath;p.Color="#40C98A";return p;}
   if(d.Conflict){p.Description="Requires attention.";p.Message="Another audio helper is running. Open Settings for details.";p.Color="#DDAA55";p.CanFix=d.ProtectionEnabled;return p;}
   if(d.CanEnable&&d.Candidates.Count>0){p.Description="Supported configuration.";p.Message="Supported Sonar microphone playback path detected.";p.Color="#288AFF";return p;}
   if(d.Status=="Unsupported Audio Setup"||d.Compatibility=="Unsupported by this preview"){p.Description="Unsupported configuration.";p.Message="This microphone route has no supported exclusion. See Settings.";p.Color="#DDAA55";return p;}
   if(d.Status=="Fix Requires Attention"){p.Description="Requires attention.";p.Message=d.Detail.StartsWith("Windows could not")?"Windows could not complete the audio check. Open Settings.":"Protection is not confirmed. Open Settings for the explanation.";p.Color=d.Detail.StartsWith("Windows could not")?"#EF6B73":"#DDAA55";return p;}
   p.Description=d.Status=="Checking audio"?"Analyzing audio routing...":"Additional diagnosis needed.";
   p.Message=d.Status=="Discord Not Running"?"Open Discord, join voice and start sharing with audio.":d.Selection.Confidence=="Suggested"?"Suggested input only. Discord's active microphone is not confirmed.":d.Status=="Waiting for screen share"?"Start Entire Screen sharing with audio, then detect again.":"No supported active duplication path is confirmed. See Settings.";
   p.Color=d.Status=="Checking audio"?"#288AFF":"#DDAA55";return p;
  }
 }
}

