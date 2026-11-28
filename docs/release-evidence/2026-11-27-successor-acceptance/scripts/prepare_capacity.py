from common import *
# Only four extra registrations: the fresh ordinary owner is the fifth allowed
# registration in this disposable process-local production bucket.
for handle in ['capacity_0','capacity_1','capacity_2','capacity_3']:
 c=a.Client(fixture['origin'],Path(fixture['ca']));c.token();c.call('auth/register','POST',{'email':handle+'@example.test','password':fixture['password'],'handle':handle,'displayName':'Capacity fixture'},201)
sql("INSERT INTO media_items SELECT (jsonb_populate_record(NULL::media_items,to_jsonb(t)||jsonb_build_object('id',gen_random_uuid(),'title','Synthetic capacity '||lpad(n::text,4,'0'),'creator_user_id',NULL,'provenance','system','media_type','movie','category_id','f11e0000-0000-0000-0000-000000000001','image_reference',NULL))).* FROM media_items t CROSS JOIN generate_series(1,1000) n WHERE t.id='"+state['public']['id']+"';")
ids=sql("SELECT id FROM media_items WHERE title LIKE 'Synthetic capacity %' ORDER BY title;").splitlines();assert len(ids)==1000
(BASE/'capacity-media-ids.json').write_text(json.dumps(ids)+'\n')
