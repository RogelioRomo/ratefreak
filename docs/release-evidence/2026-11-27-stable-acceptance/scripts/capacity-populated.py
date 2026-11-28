from common import *
# Exclude fixture registration/login setup from the production limiter measurement.
subprocess.run(command+['restart','api'],env=env,check=True)
subprocess.run(command+['up','-d','--wait','api','web','caddy'],env=env,check=True)
from concurrent.futures import ThreadPoolExecutor
import time,threading,os,statistics
# Declared before execution: six independent authenticated users, 30 four-operation
# cycles each, paced at 6.5 seconds/cycle; a small reference community burst.
policy={'users':6,'ordinary_users':5,'staff_users':1,'cycles_per_user':30,'cycle_seconds':6.5,'operations':['public media GET','own library GET','own library PUT','own library item GET'],'requests':720,'dataset':{'media_items':1003,'initial_library_entries_per_user':251,'distinct_catalog_detail_targets':180},'thresholds':{'http_errors':0,'read_p95_seconds':1.0,'write_p95_seconds':2.0},'scope':'Synthetic six-user reference single-host workload with 1,003 media items and at least 251 library entries per account. No external providers, exports, multi-node or internet latency.'}
media_ids=json.loads((BASE/'capacity-media-ids.json').read_text())
clients=[]
for handle in ['capacity_0','capacity_1','capacity_2','capacity_3','selfhost_owner','selfhost_admin']:
 c=client(handle)
 c.call('users/me/library/'+state['public']['id'],'PUT',{'status':'planned'})
 user_id=json.loads(c.call('users/me')[0])['id']
 sql("INSERT INTO user_media SELECT (jsonb_populate_record(NULL::user_media,to_jsonb(t)||jsonb_build_object('id',gen_random_uuid(),'media_item_id',m.id))).* FROM user_media t CROSS JOIN (SELECT id FROM media_items WHERE title LIKE 'Synthetic capacity %' ORDER BY title LIMIT 250) m WHERE t.user_id='"+user_id+"' AND t.media_item_id='"+state['public']['id']+"';")
 clients.append(c)
results=[];samples=[];stop=threading.Event()
ids=subprocess.check_output(command+['ps','-q','postgres','api','web'],env=env,text=True).split()
resources={'host_cpus':os.cpu_count(),'host_memory_kib':next(line.strip() for line in Path('/proc/meminfo').read_text().splitlines() if line.startswith('MemTotal:')),'containers':json.loads(subprocess.check_output(['docker','inspect']+ids,text=True))}
resources['containers']=[{'service':x['Config']['Labels']['com.docker.compose.service'],'image':x['Image'],'nano_cpus':x['HostConfig']['NanoCpus'],'memory_limit_bytes':x['HostConfig']['Memory']} for x in resources['containers']]
def sample():
 while not stop.is_set():
  value=subprocess.check_output(['docker','stats','--no-stream','--format','{{json .}}']+ids,text=True)
  samples.append({'elapsed_seconds':time.monotonic()-start,'services':[json.loads(x) for x in value.splitlines()]});stop.wait(10)
def worker(index):
 c=clients[index];local=[]
 for cycle in range(30):
  scheduled=start+cycle*6.5
  if scheduled>time.monotonic():time.sleep(scheduled-time.monotonic())
  target=media_ids[index*30+cycle]
  for kind,path,method,body in [('read','media/'+target,'GET',None),('read','users/me/library','GET',None),('write','users/me/library/'+target,'PUT',{'status':'completed' if cycle%2 else 'planned'}),('read','users/me/library/'+target,'GET',None)]:
   t=time.monotonic();error=None
   try:c.call(path,method,body)
   except Exception as exc:error=str(exc)
   local.append({'kind':kind,'seconds':time.monotonic()-t,'error':error})
 return local
start=time.monotonic();thread=threading.Thread(target=sample);thread.start()
with ThreadPoolExecutor(max_workers=6) as pool:
 for batch in pool.map(worker,range(6)):results.extend(batch)
stop.set();thread.join();elapsed=time.monotonic()-start
metrics={}
for kind in ['read','write']:
 values=sorted(x['seconds'] for x in results if x['kind']==kind);metrics[kind]={'count':len(values),'p50_seconds':statistics.median(values),'p95_seconds':values[int(len(values)*.95)-1],'max_seconds':max(values)}
errors=[x for x in results if x['error']]
out={'workload':policy,'resources':resources,'elapsed_seconds':elapsed,'requests_per_second':len(results)/elapsed,'metrics':metrics,'errors':errors,'resource_samples':samples,'passed':not errors and metrics['read']['p95_seconds']<1 and metrics['write']['p95_seconds']<2}
(BASE/'capacity-populated.json').write_text(json.dumps(out,indent=2)+'\n');assert out['passed'],str(errors[:3]);print('Defined synthetic capacity workload passed.',flush=True)
