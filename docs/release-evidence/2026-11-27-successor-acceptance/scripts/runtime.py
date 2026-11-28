from common import *
import hashlib
policy={}
for service in ['postgres','api','web','caddy']:
 value=subprocess.check_output(command+['exec','-T',service,'printenv','GLIBC_TUNABLES'],env=env,text=True).strip();assert value=='glibc.malloc.arena_max=1';policy[service]=value
for service in ['storage-init','migrate','tools']:
 value=subprocess.check_output(command+['run','--rm','--no-deps','--entrypoint','printenv',service,'GLIBC_TUNABLES'],env=env,text=True).strip();assert value=='glibc.malloc.arena_max=1';policy[service]=value
jit=sql('SHOW jit;');assert jit=='off'
ids=subprocess.check_output(command+['ps','-q','api','web','postgres','caddy'],env=env,text=True).split();running=json.loads(subprocess.check_output(['docker','inspect']+ids,text=True))
actual={item['Config']['Labels']['com.docker.compose.service']:item['Image'] for item in running};assert all(actual[x]==(images|references)[x] for x in actual)
out={'source_revision':REVISION,'all_seven_role_allocator_values':policy,'postgres_jit':jit,'application_images':images,'reference_images':references,'running_images':actual,'frozen_compose_sha256':hashlib.sha256((SOURCE/'deploy/self-host/compose.yaml').read_bytes()).hexdigest(),'production_operator_used':True,'provider_credentials_supplied':False,'persistent_installation_changed':False}
(BASE/'runtime-policy.json').write_text(json.dumps(out,indent=2)+'\n')
