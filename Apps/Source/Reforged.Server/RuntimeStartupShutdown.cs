namespace Reforged.Server;
public enum RuntimeLifecyclePhase { Stopped, Starting, Running, Quiescing, Stopping, Failed }
public sealed record RuntimeLifecycleStep(string Id,int Order,bool Required,bool Startup,bool Shutdown);
public sealed record RuntimeLifecycleAction(string StepId,bool Startup,string CorrelationId);
public sealed record RuntimeLifecycleResult(bool Success,RuntimeLifecyclePhase Phase,IReadOnlyList<RuntimeLifecycleAction> Actions,IReadOnlyList<string> Errors);
public sealed class RuntimeStartupShutdownService
{
 private readonly IReadOnlyList<RuntimeLifecycleStep> _steps;
 public RuntimeLifecyclePhase Phase { get; private set; }=RuntimeLifecyclePhase.Stopped;
 public bool AdmissionsOpen { get; private set; }
 public RuntimeStartupShutdownService(IEnumerable<RuntimeLifecycleStep> steps){_steps=steps.OrderBy(x=>x.Order).ThenBy(x=>x.Id,StringComparer.Ordinal).ToArray();if(_steps.Count==0||_steps.Select(x=>x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=_steps.Count)throw new ArgumentException("lifecycle-steps-invalid");}
 public RuntimeLifecycleResult Start(string correlationId,Func<RuntimeLifecycleAction,bool> execute){if(Phase!=RuntimeLifecyclePhase.Stopped||string.IsNullOrWhiteSpace(correlationId))return new(false,Phase,[],["startup-state-invalid"]);Phase=RuntimeLifecyclePhase.Starting;AdmissionsOpen=false;var a=new List<RuntimeLifecycleAction>();var e=new List<string>();foreach(var s in _steps.Where(x=>x.Startup)){var op=new RuntimeLifecycleAction(s.Id,true,correlationId);a.Add(op);if(!execute(op)){e.Add("startup-failed:"+s.Id);if(s.Required){Phase=RuntimeLifecyclePhase.Failed;return new(false,Phase,a,e);}}}Phase=RuntimeLifecyclePhase.Running;AdmissionsOpen=true;return new(true,Phase,a,e);}
 public RuntimeLifecycleResult Stop(string correlationId,Func<RuntimeLifecycleAction,bool> execute){if(Phase!=RuntimeLifecyclePhase.Running||string.IsNullOrWhiteSpace(correlationId))return new(false,Phase,[],["shutdown-state-invalid"]);AdmissionsOpen=false;Phase=RuntimeLifecyclePhase.Quiescing;var a=new List<RuntimeLifecycleAction>();var e=new List<string>();Phase=RuntimeLifecyclePhase.Stopping;foreach(var s in _steps.Where(x=>x.Shutdown).OrderByDescending(x=>x.Order).ThenByDescending(x=>x.Id,StringComparer.Ordinal)){var op=new RuntimeLifecycleAction(s.Id,false,correlationId);a.Add(op);if(!execute(op)){e.Add("shutdown-failed:"+s.Id);if(s.Required){Phase=RuntimeLifecyclePhase.Failed;return new(false,Phase,a,e);}}}Phase=RuntimeLifecyclePhase.Stopped;return new(true,Phase,a,e);}
 public IReadOnlyList<string> ValidateState(){var e=new List<string>();if(AdmissionsOpen&&Phase!=RuntimeLifecyclePhase.Running)e.Add("admissions-open-outside-running");if(_steps.Any(x=>string.IsNullOrWhiteSpace(x.Id)||x.Order<0))e.Add("invalid-step");return e;}
}
