using System.Text.RegularExpressions;
namespace RealmGate.Server;
public enum RuntimeTrustBoundary { Player, Script, Textblock, Builder, Administrator, Host, InternalRuntime }
public enum RuntimeSecurityOperation { Read, Execute, MutateCharacter, MutateWorld, Configure, Persist, CrossCharacter, CrossWorld, RemoteAction, FileAccess }
public sealed record RuntimeSecurityPrincipal(string PrincipalId, RuntimeTrustBoundary Boundary, IReadOnlySet<string> Roles, IReadOnlySet<string> Capabilities, bool Authenticated);
public sealed record RuntimeSecurityResource(string ResourceId, string OwnerSubsystem, RuntimeTrustBoundary MinimumBoundary, IReadOnlySet<RuntimeSecurityOperation> AllowedOperations, bool RequiresAuthentication, bool AuditRequired);
public sealed record RuntimeSecurityRequest(string RequestId, RuntimeSecurityPrincipal Principal, RuntimeSecurityResource Resource, RuntimeSecurityOperation Operation, string? TargetCharacterId, string? TargetWorldId, IReadOnlyDictionary<string,string> Inputs);
public sealed record RuntimeSecurityDecision(bool Allowed, string Code, bool AuditRequired, IReadOnlyDictionary<string,string> SanitizedInputs);
public sealed class RuntimeSecurityPolicyService
{
 private static readonly Regex SafeId=new("^[A-Za-z0-9_.:-]{1,128}$",RegexOptions.CultureInvariant);
 private readonly int _maxInputLength;
 public RuntimeSecurityPolicyService(int maxInputLength=4096){if(maxInputLength<1)throw new ArgumentOutOfRangeException(nameof(maxInputLength));_maxInputLength=maxInputLength;}
 public RuntimeSecurityDecision Authorize(RuntimeSecurityRequest request)
 {
  if(string.IsNullOrWhiteSpace(request.RequestId)||!SafeId.IsMatch(request.RequestId))return Deny("invalid-request-id",request.Resource);
  if(string.IsNullOrWhiteSpace(request.Principal.PrincipalId)||!SafeId.IsMatch(request.Principal.PrincipalId))return Deny("invalid-principal-id",request.Resource);
  if(string.IsNullOrWhiteSpace(request.Resource.ResourceId)||!SafeId.IsMatch(request.Resource.ResourceId))return Deny("invalid-resource-id",request.Resource);
  if(request.Resource.RequiresAuthentication&&!request.Principal.Authenticated)return Deny("authentication-required",request.Resource);
  if(request.Principal.Boundary<request.Resource.MinimumBoundary)return Deny("trust-boundary-denied",request.Resource);
  if(!request.Resource.AllowedOperations.Contains(request.Operation))return Deny("operation-denied",request.Resource);
  if(!HasCapability(request.Principal,request.Resource,request.Operation))return Deny("capability-denied",request.Resource);
  if(request.Operation==RuntimeSecurityOperation.CrossCharacter&&string.IsNullOrWhiteSpace(request.TargetCharacterId))return Deny("target-character-required",request.Resource);
  if(request.Operation==RuntimeSecurityOperation.CrossWorld&&string.IsNullOrWhiteSpace(request.TargetWorldId))return Deny("target-world-required",request.Resource);
  var sanitized=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  foreach(var pair in request.Inputs){if(!SafeId.IsMatch(pair.Key))return Deny("invalid-input-key",request.Resource);if(pair.Value.Length>_maxInputLength)return Deny("input-too-long",request.Resource);if(pair.Value.IndexOf('\0')>=0)return Deny("invalid-input-content",request.Resource);sanitized[pair.Key]=pair.Value.Replace("\r\n","\n").Replace('\r','\n');}
  return new(true,"authorized",request.Resource.AuditRequired,sanitized);
 }
 private static bool HasCapability(RuntimeSecurityPrincipal p,RuntimeSecurityResource r,RuntimeSecurityOperation op)
 {
  if(p.Boundary==RuntimeTrustBoundary.InternalRuntime)return true;
  var exact=$"{r.OwnerSubsystem}:{r.ResourceId}:{op}";
  var subsystem=$"{r.OwnerSubsystem}:*:{op}";
  var global=$"*:*:{op}";
  return p.Capabilities.Contains(exact)||p.Capabilities.Contains(subsystem)||p.Capabilities.Contains(global);
 }
 private static RuntimeSecurityDecision Deny(string code,RuntimeSecurityResource r)=>new(false,code,true,new Dictionary<string,string>());
 public static IReadOnlyList<string> ValidateResource(RuntimeSecurityResource r){var e=new List<string>();if(string.IsNullOrWhiteSpace(r.ResourceId)||!SafeId.IsMatch(r.ResourceId))e.Add("invalid-resource-id");if(string.IsNullOrWhiteSpace(r.OwnerSubsystem)||!SafeId.IsMatch(r.OwnerSubsystem))e.Add("invalid-owner-subsystem");if(r.AllowedOperations.Count==0)e.Add("no-allowed-operations");return e;}
}
