#!/usr/bin/env python3
"""HTTPS acceptance using disposable accounts. Explicitly requires a fresh test instance."""
import argparse
import base64
import http.cookiejar
import io
import json
from pathlib import Path
import ssl
import struct
import urllib.error
import urllib.request
import zipfile
import zlib

class Client:
    def __init__(self,origin,ca):
        self.origin=origin;self.cookies=http.cookiejar.CookieJar();self.csrf=None
        self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cookies),urllib.request.HTTPSHandler(context=ssl.create_default_context(cafile=str(ca))))
    def call(self,path,method='GET',body=None,expected=200,headers=None):
        values={'Origin':self.origin};values.update(headers or {})
        if method!='GET':
            if self.csrf:values['X-CSRF-TOKEN']=self.csrf
            if not isinstance(body,bytes):body=json.dumps(body or {}).encode();values['Content-Type']='application/json'
        request=urllib.request.Request(self.origin+'/api/v1/'+path,data=body,method=method,headers=values)
        try:response=self.opener.open(request,timeout=310)
        except urllib.error.HTTPError as error:response=error
        content=response.read()
        assert response.status==expected,f'{method} {path}: expected {expected}, got {response.status}'
        return content,response.headers
    def token(self):self.csrf=json.loads(self.call('auth/csrf')[0])['requestToken']
    def login(self,handle,password):
        self.token();self.call('auth/login','POST',{'email':handle+'@example.test','password':password});self.token()

def seed(root,origin,ca,resume=False):
    admin=Client(origin,ca);required=json.loads(admin.call('setup/status')[0])['required']
    assert required or resume, "Acceptance seed requires a fresh instance; use --resume only for this interrupted disposable fixture."
    password='Disposable-selfhost-password-73'
    admin.token()
    if required:
        response,headers=admin.call('setup','POST',{'setupSecret':(root/'secrets/setup').read_text().strip(),'email':'selfhost_admin@example.test','password':password,'handle':'selfhost_admin','displayName':'Disposable Admin'},201)
        admin_id=json.loads(response)['id']
    admin.login('selfhost_admin',password)
    admin_id=json.loads(admin.call('users/me')[0])['id']
    cookies=list(admin.cookies)
    assert any(c.name=='__Host-ratefreak.session' and c.secure and any(key.lower()=='httponly' for key in c._rest) for c in cookies)
    admin.call('users/me',headers={'X-RateFreak-Ingress-Key':'forged','X-RateFreak-Ingress-IP':'203.0.113.99','X-Forwarded-For':'203.0.113.99'})
    # CSRF and Origin checks are exercised through the real edge.
    saved=admin.csrf;admin.csrf=None;admin.call('auth/logout','POST',{},400);admin.csrf=saved
    admin.call('auth/logout','POST',{},403,{'Origin':'https://foreign.example'})
    owner=Client(origin,ca);owner.token()
    result=json.loads(owner.call('auth/register','POST',{'email':'selfhost_owner@example.test','password':password,'handle':'selfhost_owner','displayName':'Disposable Owner'},201)[0])
    owner_id=result['id'];owner.login('selfhost_owner',password)
    category=json.loads(owner.call('categories','POST',{'name':'Disposable acceptance '+owner_id[-8:],'scope':'public'},201)[0])
    state={'owner':owner_id,'admin':admin_id,'password':password,'origin':origin}
    for visibility in ['private','public','held']:
        item=json.loads(owner.call('media','POST',{'title':'Disposable '+visibility,'description':'Acceptance fixture','categoryId':category['id'],'visibility':'public' if visibility=='held' else visibility},201)[0])
        def chunk(kind,payload):return struct.pack('!I',len(payload))+kind+payload+struct.pack('!I',zlib.crc32(kind+payload)&0xffffffff)
        image=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('!IIBBBBB',2,2,8,6,0,0,0))+chunk(b'IDAT',zlib.compress((b'\0'+bytes([20,90,170,255])*2)*2))+chunk(b'IEND',b'')
        boundary='selfhost-fixed-test-boundary'
        body=(f'--{boundary}\r\nContent-Disposition: form-data; name="expectedRevision"\r\n\r\n{item["revision"]}\r\n--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="fixture.png"\r\nContent-Type: image/png\r\n\r\n').encode()+image+f'\r\n--{boundary}--\r\n'.encode()
        item=json.loads(owner.call('media/'+item['id']+'/artwork','PUT',body,200,{'Content-Type':'multipart/form-data; boundary='+boundary})[0])
        if visibility=='held':
            target=json.loads(admin.call('moderation/media/'+item['id'])[0])
            admin.call('moderation/media/'+item['id']+'/visibility','POST',{'hidden':True,'reason':'Disposable moderation hold','expectedTargetUpdatedAt':target['updatedAt']},204)
        state[visibility]=item
    report=json.loads(admin.call('users/'+owner_id+'/reports','POST',{'reason':'spam','details':'Disposable profile report'},201)[0])
    profile=json.loads(admin.call('moderation/profiles/'+owner_id)[0])
    admin.call('moderation/profiles/'+owner_id+'/visibility','POST',{'hidden':True,'reason':'Disposable profile hold','expectedTargetUpdatedAt':profile['updatedAt']},204)
    state['profileReport']=report['id']
    anonymous=Client(origin,ca)
    anonymous.call('media/'+state['private']['id'],expected=404)
    anonymous.call('media/'+state['held']['id'],expected=404)
    owner.call('users/me/data/confirm','POST',{'operation':'export','password':password},204)
    exported,headers=owner.call('users/me/data/export','POST')
    assert 'no-store' in headers['Cache-Control'] and 'attachment' in headers['Content-Disposition']
    with zipfile.ZipFile(io.BytesIO(exported)) as archive:
        assert 'account.json' in archive.namelist() and len([n for n in archive.namelist() if n.startswith('artwork/')])==3
    (root/'acceptance.json').write_text(json.dumps(state,indent=2)+'\n');(root/'acceptance.json').chmod(0o600)
    print('HTTPS setup, secure cookies, CSRF/Origin rejection, forged-header overwrite, ordinary registration, image decode/storage/privacy and ZIP export passed.')

def delete(root,origin,ca):
    state=json.loads((root/'acceptance.json').read_text());owner=Client(origin,ca);owner.login('selfhost_owner',state['password'])
    owner.call('users/me/data/confirm','POST',{'operation':'delete','password':state['password']},204)
    owner.call('users/me/data/delete','POST',{'handle':'selfhost_owner'},204)
    owner.call('users/me',expected=401)
    anonymous=Client(origin,ca);anonymous.call('media/'+state['private']['id'],expected=404)
    assert json.loads(anonymous.call('media/'+state['public']['id'])[0])['creatorUserId'] is None
    print('Confirmed deletion, session rejection, private erasure and public ownership detachment passed.')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--instance-dir',type=Path,required=True);parser.add_argument('--ca',type=Path,required=True);parser.add_argument('--confirm-disposable',action='store_true',required=True);parser.add_argument('--resume',action='store_true');parser.add_argument('phase',choices=['seed','delete']);args=parser.parse_args()
    root=args.instance_dir.resolve();origin=json.loads((root/'instance.json').read_text())['origin']
    if args.phase=='seed':seed(root,origin,args.ca,args.resume)
    else:delete(root,origin,args.ca)
