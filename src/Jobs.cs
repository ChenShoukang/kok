using System;
using System.Linq;
using System.Collections.Generic;
namespace kok {
 public class Job {public string id,module,sourceId,title,state,detail,unit="step";public double done,total;public bool resumable,pausable=true,controllable=true;public Need requires;public string[] itemIds=new string[0],opIds=new string[0];}
 public sealed partial class Jobs {
  readonly Dictionary<string,Func<List<Job>>> providers=new Dictionary<string,Func<List<Job>>>();readonly Action<string,object> notify;
  public Jobs(string dataDir,Action<string,object> notify){this.notify=notify??((n,v)=>{});Initialize(dataDir);}
  public void Add(string module,Func<List<Job>> provider){providers.Add(module,provider);}
  public List<Job> Snapshot(){return providers.Values.SelectMany(p=>p()).Concat(ManagedSnapshot()).ToList();}
  public void Changed(){notify("job.changed",null);}
 }
 public sealed partial class DownloadManager {
  public List<Job> JobSnapshot(){lock(gate)return store.jobs.Select(j=>new Job{id="downloads:"+j.id,module="downloads",sourceId=j.id,title=String.IsNullOrWhiteSpace(j.title)?Source(j.url):j.title,state=j.status=="completed"?"done":j.status=="error"?"failed":j.status=="cancelled"?"cancelled":j.status=="paused"?"paused":j.status=="queued"?"queued":"running",done=j.received,total=j.total,unit="bytes",resumable=new[]{"paused","error"}.Contains(j.status),requires=Need.Network|Need.NormalLibrary,detail="downloads"}).ToList();}
 }
}
