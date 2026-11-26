#include <stdint.h>
#include <stdio.h>
#include <dlfcn.h>
#include <unistd.h>
#include <glob.h>
#include <stdlib.h>
#include <sys/stat.h>
typedef uint32_t U;
int main(void){
 void *g=dlopen("libgssapi_krb5.so.2",RTLD_NOW);if(!g){puts("{\"error\":\"dlopen failed\"}");return 1;}
 U(*acquire)(U*,void*,U,void*,int,void**,void*,void*)=dlsym(g,"gss_acquire_cred");
 U(*release)(U*,void**)=dlsym(g,"gss_release_cred");
 if(!acquire||!release)return 2;
 printf("{\"uid\":%d,\"credentials\":[",getuid());
 for(int usage=1;usage<=2;usage++){U minor=0,major;void*cred=NULL;major=acquire(&minor,NULL,0,NULL,usage,&cred,NULL,NULL);printf("%s{\"usage\":%d,\"major\":%u,\"minor\":%u,\"handle_present\":%s}",usage==1?"":",",usage,major,minor,cred?"true":"false");if(cred)release(&minor,&cred);}
 printf("],\"paths\":{");const char*paths[]={"/etc/krb5.keytab","/etc/krb5.conf","/etc/postgresql-common/pg_service.conf","/var/lib/postgresql/.pg_service.conf","/usr/sbin/krb5kdc","/usr/sbin/kadmind","/usr/sbin/slapd","/usr/bin/mdb_load"};
 for(int i=0;i<8;i++){struct stat s;printf("%s\"%s\":%s",i?",":"",paths[i],lstat(paths[i],&s)==0?"true":"false");}
 printf("},\"environment_present\":{");const char*env[]={"KRB5_CONFIG","KRB5_KTNAME","KRB5CCNAME","PGSERVICE","PGSERVICEFILE","LDAPCONF"};for(int i=0;i<6;i++)printf("%s\"%s\":%s",i?",":"",env[i],getenv(env[i])?"true":"false");puts("}}");return 0;
}
