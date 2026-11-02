#include "DotNetHost.h"
#include <dlfcn.h>
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

// Official .NET hostfxr native hosting ABI. Keep the runtime inside the Quick Look
// extension: its sandbox intentionally disallows launching a child process.
typedef int32_t (*initialize_fn)(int,const char **,const void *,void **);
typedef int32_t (*get_delegate_fn)(void *,int,void **);
typedef int32_t (*close_fn)(void *);
typedef int32_t (*load_fn)(const char *,const char *,const char *,const char *,void *,void **);
typedef char *(*invoke_fn)(const char *);
typedef void (*release_fn)(char *);
typedef struct { size_t size; const char *host_path; const char *dotnet_root; } init_parameters;
static pthread_mutex_t gate=PTHREAD_MUTEX_INITIALIZER;
static invoke_fn invoke;
static release_fn release;
static void *library;
static int last_error;
static int initialize(const char *directory) {
    if(invoke)return 1;
    char library_path[4096],assembly[4096],host[4096];
    snprintf(library_path,sizeof(library_path),"%s/libhostfxr.dylib",directory);
    snprintf(assembly,sizeof(assembly),"%s/QuickLookRT.Worker.dll",directory);
    snprintf(host,sizeof(host),"%s/QuickLookRT.Worker",directory);
    setenv("DOTNET_EnableDiagnostics","0",1);
    setenv("DOTNET_SYSTEM_GLOBALIZATION_INVARIANT","1",1);
    library=dlopen(library_path,RTLD_NOW|RTLD_LOCAL);
    if(!library){last_error=1;return 0;}
    initialize_fn init=(initialize_fn)dlsym(library,"hostfxr_initialize_for_dotnet_command_line");
    get_delegate_fn get=(get_delegate_fn)dlsym(library,"hostfxr_get_runtime_delegate");
    close_fn close=(close_fn)dlsym(library,"hostfxr_close");
    if(!init||!get||!close){last_error=2;return 0;}
    void *context=NULL,*loader=NULL;
    init_parameters parameters={sizeof(parameters),host,directory};
    const char *argv[]={assembly};
    int rc=init(1,argv,&parameters,&context);
    if(rc<0||!context){last_error=rc;return 0;}
    rc=get(context,5,&loader);close(context);
    if(rc<0||!loader){last_error=rc;return 0;}
    load_fn load=(load_fn)loader;
    void *entry=NULL,*free_entry=NULL;
    rc=load(assembly,"QuickLookRT.Mac.NativeBridge, QuickLookRT.Worker","Invoke",(const char *)-1,NULL,&entry);
    if(rc<0||!entry){last_error=rc;return 0;}
    rc=load(assembly,"QuickLookRT.Mac.NativeBridge, QuickLookRT.Worker","Release",(const char *)-1,NULL,&free_entry);
    if(rc<0||!free_entry){last_error=rc;return 0;}
    release=(release_fn)free_entry;invoke=(invoke_fn)entry;return 1;
}
char *qlrt_call(const char *request,const char *directory) {
    pthread_mutex_lock(&gate);int ready=initialize(directory);pthread_mutex_unlock(&gate);
    return ready?invoke(request):NULL;
}
void qlrt_release(char *reply) { if(reply&&release)release(reply); }
void qlrt_close(const char *session) {
    pthread_mutex_lock(&gate);invoke_fn fn=invoke;pthread_mutex_unlock(&gate);
    if(!fn)return;
    char request[256];snprintf(request,sizeof(request),"{\"id\":-1,\"op\":\"close\",\"session\":\"%s\"}",session);
    char *result=fn(request);if(result)release(result);
}
int qlrt_host_error(void) {return last_error;}
