import ast,hashlib,json,pathlib,runpy,sys,importlib.util
p=pathlib.Path('/app/entrypoint.py');data=p.read_bytes();tree=ast.parse(data)
runpy.run_path(str(p),run_name='review_entrypoint')
out={'entrypoint_sha256':hashlib.sha256(data).hexdigest(),'entrypoint_imports':sorted({n for a in ast.walk(tree) for n in ([x.name for x in a.names] if isinstance(a,ast.Import) else [a.module] if isinstance(a,ast.ImportFrom) else [])}),'xml_modules_loaded_by_entrypoint':[m for m in sys.modules if m=='pyexpat' or m.startswith(('xml.','_elementtree'))],'gpgtar_exists':pathlib.Path('/usr/bin/gpgtar').exists(),'runtime_uid':__import__('os').getuid()}
out['xml_modules_available']={name:importlib.util.find_spec(name) is not None for name in ['pyexpat','xml.etree.ElementTree']}
print(json.dumps(out,indent=2))
