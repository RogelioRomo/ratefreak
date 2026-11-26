#define _GNU_SOURCE
#include <dlfcn.h>
#include <iconv.h>
#include <stdio.h>
iconv_t iconv_open(const char *to, const char *from) {
  iconv_t (*next)(const char *, const char *) = dlsym(RTLD_NEXT, "iconv_open");
  fprintf(stderr, "ICONV_OPEN to=%s from=%s\n", to, from);
  return next(to, from);
}
