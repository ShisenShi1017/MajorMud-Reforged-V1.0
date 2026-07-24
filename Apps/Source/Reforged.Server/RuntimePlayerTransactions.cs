using System.Text.Json;
namespace Reforged.Server;
public enum RuntimePlayerTransactionState { Offered, Accepted, Committed, Cancelled, Expired, Failed }
public enum RuntimePlayerTransferKind { Item, Currency }
public sealed record RuntimeTransferLine(RuntimePlayerTransferKind Kind,string AssetId,long Amount,bool QuestItem=false,bool LimitedItem=false,bool BoundItem=false);
public sealed record RuntimePlayerTransactionPolicy(int MaximumLines,TimeSpan OfferLifetime,bool AllowQuestItems,bool AllowLimitedItems,bool AllowBoundItems);
public sealed record RuntimeTransactionRequest(string TransactionId,string FromCharacterId,string ToCharacterId,IReadOnlyList<RuntimeTransferLine> Offered,IReadOnlyList<RuntimeTransferLine> Requested,DateTimeOffset ExpiresUtc,long Revision);
public sealed record RuntimeTransactionOwnerRequest(string Owner,string Operation,string CharacterId,string AssetId,long Amount,string CorrelationId);
public sealed record RuntimeTransactionResult(bool Accepted,string Code,RuntimePlayerTransactionState State,IReadOnlyList<RuntimeTransactionOwnerRequest> OwnerRequests,long Revision);
public sealed class RuntimePlayerTransactionService
{
 private readonly RuntimePlayerTransactionPolicy _policy; private readonly Dictionary<string,(RuntimeTransactionRequest Request,RuntimePlayerTransactionState State)> _transactions=new(StringComparer.OrdinalIgnoreCase);
 public RuntimePlayerTransactionService(RuntimePlayerTransactionPolicy policy){if(policy.MaximumLines<=0||policy.OfferLifetime<=TimeSpan.Zero)throw new ArgumentException("invalid-transaction-policy");_policy=policy;}
 public RuntimeTransactionResult Offer(string id,string from,string to,IReadOnlyList<RuntimeTransferLine> offered,IReadOnlyList<RuntimeTransferLine> requested,DateTimeOffset now)
 {
  if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(from)||string.IsNullOrWhiteSpace(to)||string.Equals(from,to,StringComparison.OrdinalIgnoreCase))return Reject("invalid-parties");
  if(_transactions.ContainsKey(id))return Reject("duplicate-transaction"); if(offered.Count+requested.Count==0||offered.Count+requested.Count>_policy.MaximumLines)return Reject("invalid-line-count");
  foreach(var line in offered.Concat(requested)){if(string.IsNullOrWhiteSpace(line.AssetId)||line.Amount<=0)return Reject("invalid-transfer-line");if(line.QuestItem&&!_policy.AllowQuestItems)return Reject("quest-item-restricted");if(line.LimitedItem&&!_policy.AllowLimitedItems)return Reject("limited-item-restricted");if(line.BoundItem&&!_policy.AllowBoundItems)return Reject("bound-item-restricted");}
  var req=new RuntimeTransactionRequest(id,from,to,offered,requested,now+_policy.OfferLifetime,1);_transactions[id]=(req,RuntimePlayerTransactionState.Offered);return new(true,"offered",RuntimePlayerTransactionState.Offered,Array.Empty<RuntimeTransactionOwnerRequest>(),1);
 }
 public RuntimeTransactionResult Accept(string id,string actor,DateTimeOffset now)
 {
  if(!_transactions.TryGetValue(id,out var x))return Reject("unknown-transaction");if(x.State!=RuntimePlayerTransactionState.Offered)return Reject("invalid-state");if(!string.Equals(actor,x.Request.ToCharacterId,StringComparison.OrdinalIgnoreCase))return Reject("not-recipient");if(now>=x.Request.ExpiresUtc){_transactions[id]=(x.Request,RuntimePlayerTransactionState.Expired);return Reject("offer-expired",RuntimePlayerTransactionState.Expired,x.Request.Revision+1);}var req=x.Request with{Revision=x.Request.Revision+1};_transactions[id]=(req,RuntimePlayerTransactionState.Accepted);return new(true,"accepted",RuntimePlayerTransactionState.Accepted,BuildOwnerRequests(req),req.Revision);
 }
 public RuntimeTransactionResult Commit(string id,long expectedRevision)
 {
  if(!_transactions.TryGetValue(id,out var x))return Reject("unknown-transaction");if(x.State!=RuntimePlayerTransactionState.Accepted)return Reject("invalid-state");if(x.Request.Revision!=expectedRevision)return Reject("revision-conflict");var req=x.Request with{Revision=x.Request.Revision+1};_transactions[id]=(req,RuntimePlayerTransactionState.Committed);return new(true,"committed",RuntimePlayerTransactionState.Committed,Array.Empty<RuntimeTransactionOwnerRequest>(),req.Revision);
 }
 public RuntimeTransactionResult Cancel(string id,string actor){if(!_transactions.TryGetValue(id,out var x))return Reject("unknown-transaction");if(x.State is RuntimePlayerTransactionState.Committed or RuntimePlayerTransactionState.Cancelled)return Reject("invalid-state");if(!string.Equals(actor,x.Request.FromCharacterId,StringComparison.OrdinalIgnoreCase)&&!string.Equals(actor,x.Request.ToCharacterId,StringComparison.OrdinalIgnoreCase))return Reject("not-party");var req=x.Request with{Revision=x.Request.Revision+1};_transactions[id]=(req,RuntimePlayerTransactionState.Cancelled);return new(true,"cancelled",RuntimePlayerTransactionState.Cancelled,Array.Empty<RuntimeTransactionOwnerRequest>(),req.Revision);}
 public RuntimeTransactionResult Disconnect(string id,string actor)=>Cancel(id,actor);
 public IReadOnlyList<string> ValidateState()=>_transactions.Where(kv=>kv.Value.Request.Revision<=0||kv.Value.Request.Offered.Concat(kv.Value.Request.Requested).Any(x=>x.Amount<=0)).Select(kv=>"invalid-transaction:"+kv.Key).ToArray();
 public string ToJson()=>JsonSerializer.Serialize(_transactions,new JsonSerializerOptions{WriteIndented=true});
 private static IReadOnlyList<RuntimeTransactionOwnerRequest> BuildOwnerRequests(RuntimeTransactionRequest r){var list=new List<RuntimeTransactionOwnerRequest>();foreach(var x in r.Offered)list.Add(new(x.Kind==RuntimePlayerTransferKind.Currency?"Economy":"CharacterInventory","reserve-transfer",r.FromCharacterId,x.AssetId,x.Amount,r.TransactionId));foreach(var x in r.Requested)list.Add(new(x.Kind==RuntimePlayerTransferKind.Currency?"Economy":"CharacterInventory","reserve-transfer",r.ToCharacterId,x.AssetId,x.Amount,r.TransactionId));return list;}
 private static RuntimeTransactionResult Reject(string c,RuntimePlayerTransactionState s=RuntimePlayerTransactionState.Failed,long rev=0)=>new(false,c,s,Array.Empty<RuntimeTransactionOwnerRequest>(),rev);
}
