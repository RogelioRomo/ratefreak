import subprocess,json
from pathlib import Path
b=Path(__file__).resolve().parent
i="postgres:17-trixie@sha256:87c8b42f423b45d5247e6aa8fdb2e7762fec164c68a50fe3eb07e7bfcaf4dbcf"
d=json.loads(subprocess.check_output(["docker","image","inspect",i],text=True))[0]
cmd=["docker","run","--rm","--network","none","--read-only","--cap-drop","ALL","--entrypoint","/bin/sh",i,"-c",'for p in /etc/editrc /root/.editrc /var/lib/postgresql/.editrc /etc/krb5.conf /etc/gss/mech; do if test -e "$p"; then echo "$p exists"; else echo "$p absent"; fi; done']
s=subprocess.check_output(cmd,text=True)
o={"image_reference":i,"image_env":d["Config"].get("Env"),"entrypoint":d["Config"].get("Entrypoint"),"command":d["Config"].get("Cmd"),"probe_command":cmd,"path_checks":s.splitlines()}
(b/"pg-config-metadata.json").write_text(json.dumps(o,indent=2)+"\n")
print(s)
