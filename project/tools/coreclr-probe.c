/* Isolated Intel test host using the bundled CoreCLR. SPDX-License-Identifier: MIT */
#define _GNU_SOURCE
#include <dlfcn.h>
#include <dirent.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <limits.h>
static char *tpa_list(const char *dir) {
    DIR *d=opendir(dir);if(!d)return NULL;
    size_t used=0,cap=8192;char *buf=malloc(cap);struct dirent *e;
    if(!buf){closedir(d);return NULL;}buf[0]=0;
    while((e=readdir(d))){
        size_t n=strlen(e->d_name);if(n<4||strcmp(e->d_name+n-4,".dll"))continue;
        size_t need=used+strlen(dir)+n+3;
        if(need>cap){while(cap<need)cap*=2;char *p=realloc(buf,cap);if(!p){free(buf);closedir(d);return NULL;}buf=p;}
        used+=(size_t)snprintf(buf+used,cap-used,"%s/%s:",dir,e->d_name);
    }
    closedir(d);if(used)buf[used-1]=0;return buf;
}
int main(int argc,char **argv){
    /* runtime directory, managed directory, assembly, type, method */
    if(argc!=6){fprintf(stderr,"Invalid test-host arguments\n");return 2;}
    char runtime[PATH_MAX],native_dirs[PATH_MAX*3];
    if(snprintf(runtime,sizeof(runtime),"%s/libcoreclr.dylib",argv[1])>=(int)sizeof(runtime))return 6;
    if(snprintf(native_dirs,sizeof(native_dirs),"%s:%s",argv[1],argv[2])>=(int)sizeof(native_dirs))return 6;
    void *clr=dlopen(runtime,RTLD_NOW|RTLD_LOCAL);if(!clr){fprintf(stderr,"coreclr: %s\n",dlerror());return 7;}
    int (*initialize)(const char*,const char*,int,const char**,const char**,void**,unsigned int*);
    int (*make_delegate)(void*,unsigned int,const char*,const char*,const char*,void**);
    int (*shutdown)(void*,unsigned int,int*);
#define LOAD(fn,name) do{void *p=dlsym(clr,name);if(!p){fprintf(stderr,"Missing %s\n",name);return 8;}memcpy(&fn,&p,sizeof(fn));}while(0)
    LOAD(initialize,"coreclr_initialize");LOAD(make_delegate,"coreclr_create_delegate");LOAD(shutdown,"coreclr_shutdown_2");
    char *tpa=tpa_list(argv[1]);if(!tpa)return 9;
    const char *keys[]={"TRUSTED_PLATFORM_ASSEMBLIES","APP_PATHS","APP_NI_PATHS","NATIVE_DLL_SEARCH_DIRECTORIES"};
    const char *values[]={tpa,argv[2],argv[2],native_dirs};
    void *host=NULL;unsigned int domain=0;
    int hr=initialize(argv[0],"deception-isolated-model-tests",4,keys,values,&host,&domain);
    if(hr<0){fprintf(stderr,"coreclr_initialize: 0x%x\n",(unsigned)hr);free(tpa);return 10;}
    void *entry=NULL;hr=make_delegate(host,domain,argv[3],argv[4],argv[5],&entry);
    if(hr<0||!entry){fprintf(stderr,"coreclr_create_delegate: 0x%x\n",(unsigned)hr);return 11;}
    int (*run)(void);memcpy(&run,&entry,sizeof(run));int result=run();
    int latched=0;hr=shutdown(host,domain,&latched);free(tpa);
    if(hr<0){fprintf(stderr,"coreclr_shutdown: 0x%x\n",(unsigned)hr);return 12;}
    /* Do not dlclose CoreCLR after CLR shutdown. They
       may own finalizer/delegate state; process exit releases this test process. */
    return result;
}
