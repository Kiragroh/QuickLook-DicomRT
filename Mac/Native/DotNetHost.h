#ifndef QUICKLOOKRT_HOST_H
#define QUICKLOOKRT_HOST_H
char *qlrt_call(const char *request, const char *directory);
void qlrt_release(char *reply);
void qlrt_close(const char *session);
int qlrt_host_error(void);
#endif
