from common import *
os.environ['TMPDIR']=str(BASE)
root,result=a.run(images,BASE/'maintenance-fresh',keep=True)
(BASE/'maintenance-root.txt').write_text(str(root)+'\n')
fixture=json.loads((root/'browser-fixture.json').read_text())
for handle in ['capacity_0','capacity_3']:
 c=a.Client(fixture['origin'],Path(fixture['ca']));c.token();c.call('auth/register','POST',{'email':handle+'@example.test','password':fixture['password'],'handle':handle,'displayName':'Maintenance fixture'},201)
