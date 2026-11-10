#!/usr/bin/env python3
"""Five-minute edge/export timeout and disconnect proof against a controlled upstream."""
import argparse
import hashlib
import http.server
import json
import os
from pathlib import Path
import ssl
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request


def server():
    state={};gate=threading.Lock()
    def record(name):
        with gate:
            state[name]=True;Path('/proof/state.json').write_text(json.dumps(state))
    class Handler(http.server.BaseHTTPRequestHandler):
        def log_message(self,*args):pass
        def do_GET(self):
            self.send_response(200);self.send_header('Content-Type','application/json');self.end_headers();self.wfile.write(b'{"required":false,"configured":true}')
        def do_POST(self):
            body=json.loads(self.rfile.read(int(self.headers['Content-Length'])))
            if body['case']=='delay':
                record('delayStarted');time.sleep(302)
                try:self.send_response(200);self.end_headers();self.wfile.write(b'late')
                except (BrokenPipeError,ConnectionResetError):record('delayCancelled')
            else:
                self.send_response(200);self.send_header('Content-Type','application/zip');self.end_headers()
                try:
                    for _ in range(1200):self.wfile.write(b'PK'+b'x'*32766);self.wfile.flush();time.sleep(.05)
                except (BrokenPipeError,ConnectionResetError):record('streamCancelled')
    http.server.ThreadingHTTPServer(('0.0.0.0',8080),Handler).serve_forever()


def proof(root):
    from ratefreak import initialize,compose,execute,PACKAGE
    root=Path(os.path.abspath(root));config=initialize(root,'https://localhost:8443',True)
    base,env=compose(root,config)
    execute(base+['run','--rm','--no-deps','storage-init'],env)
    spec={
      'services':{
        'backend':{'image':'ratefreak-tools:local','user':'0:0','entrypoint':['python3','/edge_acceptance.py','--server'],'volumes':[str(PACKAGE/'edge_acceptance.py')+':/edge_acceptance.py:ro','proof:/proof']},
        'web':{'image':'ratefreak-web:local','environment':{'RATEFREAK_API_URL':'http://backend:8080','RATEFREAK_WEB_ORIGIN':'https://localhost:8443','RATEFREAK_CLIENT_IP_MODE':'trusted-proxy','RATEFREAK_ALLOW_PRIVATE_API_HTTP':'true'},'volumes':[str(root/'runtime/web')+':/run/ratefreak:ro']},
        'edge':{'image':'caddy:2@sha256:ce773ec7a742ce15ee6bf2bb5f65882b9e2a4487b10733e6f1484f86e6b5d8bc','entrypoint':['/bin/sh','/edge-entrypoint.sh'],'ports':['127.0.0.1:8443:8443'],'environment':{'RATEFREAK_PUBLIC_ORIGIN':'https://localhost:8443','RATEFREAK_CADDY_TLS':'tls internal'},'volumes':[str(PACKAGE/'Caddyfile')+':/etc/caddy/Caddyfile:ro',str(PACKAGE/'edge-entrypoint.sh')+':/edge-entrypoint.sh:ro',str(root/'runtime/web/ingress')+':/run/ratefreak/ingress:ro','ca:/data']}
      },'volumes':{'proof':{},'ca':{}}}
    path=root/'edge-proof.json';path.write_text(json.dumps(spec));path.chmod(0o600)
    command=['docker','compose','--project-name','ratefreak-edge-proof-'+hashlib.sha256(str(root).encode()).hexdigest()[:10],'-f',str(path)]
    if execute(command+['ps','--all','-q'],capture=True).strip():raise RuntimeError('Disposable proof project already has containers; inspect/clean it explicitly before another run.')
    execute(command+['up','-d','--wait'])
    ca=root/'test-ca.crt'
    for attempt in range(30):
        result=subprocess.run(command+['cp','edge:/data/caddy/pki/authorities/local/root.crt',str(ca)],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
        if result.returncode==0:break
        time.sleep(1)
    context=ssl.create_default_context(cafile=str(ca))
    def request(case):
        value=urllib.request.Request('https://localhost:8443/api/v1/users/me/data/export',data=json.dumps({'case':case}).encode(),headers={'Origin':'https://localhost:8443','Content-Type':'application/json','X-CSRF-TOKEN':'edge-test'},method='POST')
        try:return urllib.request.urlopen(value,context=context,timeout=315)
        except urllib.error.HTTPError as error:return error
    # Wait for the actual Next server without bypassing TLS validation.
    for attempt in range(30):
        try:
            with urllib.request.urlopen('https://localhost:8443/api/v1/setup/status',context=context,timeout=3) as response:
                if response.status==200:break
        except (OSError,urllib.error.URLError):pass
        time.sleep(1)
    stream=request('cancel');assert stream.status==200;assert stream.read(1024).startswith(b'PK');stream.close()
    for attempt in range(30):
        output=subprocess.run(command+['exec','-T','backend','cat','/proof/state.json'],capture_output=True,text=True)
        if output.returncode==0 and json.loads(output.stdout).get('streamCancelled'):break
        time.sleep(.25)
    else:raise AssertionError('Client disconnect did not cancel the controlled upstream')
    started=time.monotonic()
    delayed=request('delay');elapsed=time.monotonic()-started
    assert delayed.status==504 and 290<=elapsed<=315,(delayed.status,elapsed)
    result={'tlsVerified':True,'controlledUpstream':True,'disconnectPropagated':True,'exportDeadlineSeconds':round(elapsed,2),'deadlineStatus':504}
    (root/'results.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result))
    execute(command+['down','--volumes'])

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--server',action='store_true');parser.add_argument('--instance-dir',type=Path);args=parser.parse_args()
    if args.server:server()
    elif args.instance_dir:proof(args.instance_dir)
    else:parser.error('Provide --instance-dir for a dedicated disposable proof directory')
