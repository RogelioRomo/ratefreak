#define _GNU_SOURCE
#include <errno.h>
#include <gnu/libc-version.h>
#include <malloc.h>
#include <pthread.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

enum { WORKERS = 16 };
static pthread_barrier_t barrier;
static uintptr_t stack_addresses[WORKERS];

static void *allocate(void *arg) {
    size_t index = (size_t)arg;
    char marker;
    stack_addresses[index] = (uintptr_t)&marker;
    void *blocks[64];
    for (size_t i = 0; i < 64; i++) {
        blocks[i] = malloc(16384);
        if (!blocks[i]) abort();
        memset(blocks[i], 1, 16384);
    }
    pthread_barrier_wait(&barrier);
    pthread_barrier_wait(&barrier);
    for (size_t i = 0; i < 64; i++) free(blocks[i]);
    return NULL;
}

static void *stack_only(void *arg) {
    char marker;
    *(uintptr_t *)arg = (uintptr_t)&marker;
    return NULL;
}

static int mapped(uintptr_t address) {
    FILE *maps = fopen("/proc/self/maps", "r");
    if (!maps) abort();
    char line[1024];
    unsigned long start, end;
    int result = 0;
    while (fgets(line, sizeof(line), maps)) {
        if (sscanf(line, "%lx-%lx", &start, &end) == 2 && address >= start && address < end) {
            result = 1;
            break;
        }
    }
    fclose(maps);
    return result;
}

int main(void) {
    pthread_t workers[WORKERS];
    if (pthread_barrier_init(&barrier, NULL, WORKERS + 1)) return 2;
    for (size_t i = 0; i < WORKERS; i++)
        if (pthread_create(&workers[i], NULL, allocate, (void *)i)) return 3;
    pthread_barrier_wait(&barrier);
    char *xml = NULL;
    size_t length = 0;
    FILE *out = open_memstream(&xml, &length);
    if (!out || malloc_info(0, out) || fclose(out)) return 4;
    size_t arenas = 0;
    for (char *p = xml; (p = strstr(p, "<heap nr=")); p++) arenas++;
    free(xml);
    pthread_barrier_wait(&barrier);
    for (size_t i = 0; i < WORKERS; i++)
        if (pthread_join(workers[i], NULL)) return 5;
    uintptr_t previous = 0;
    size_t reused = 0, retained = 0;
    for (size_t i = 0; i < 32; i++) {
        uintptr_t current = 0;
        pthread_t thread;
        if (pthread_create(&thread, NULL, stack_only, &current) || pthread_join(thread, NULL)) return 6;
        reused += i && current == previous;
        retained += mapped(current);
        previous = current;
    }
    printf("{\"libc\":\"%s\",\"workers\":%d,\"arenas\":%zu,\"sequential_stack_reuses\":%zu,\"post_join_stack_mapped\":%zu}\n",
           gnu_get_libc_version(), WORKERS, arenas, reused, retained);
    pthread_barrier_destroy(&barrier);
    return 0;
}
