// Synthetic executable-code regression, no game or loader data.
// cl /EHsc dobby-branch-test.cpp /Fe:dobby-branch-test.exe
#include <windows.h>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <limits.h>

using Fn = int (*)(int, int);
static Fn original;
static int replacement(int a, int b) { return original(a,b) + 100; }
using Hook = int (*)(void*,void*,void**);
using Destroy = int (*)(void*);

int main(int argc,char** argv) {
  if(argc!=2) return 2;
  auto lib=LoadLibraryA(argv[1]);
  if(!lib) { printf("LoadLibrary failed: %lu\n",GetLastError()); return 2; }
  auto hook=(Hook)GetProcAddress(lib,"DobbyHook");
  auto destroy=(Destroy)GetProcAddress(lib,"DobbyDestroy");
  if(!hook||!destroy) return 2;
  const int pairs[][2]={{0,0},{1,0},{0,1},{-1,1},{INT_MIN,1},{INT_MAX,-1},{0x55,0},{0x54,0}};
  int cases=0, failures=0;
  for(int backward=0;backward<2;backward++) for(int cc=0;cc<=32;cc++) {
    auto page=(uint8_t*)VirtualAlloc(nullptr,4096,MEM_RESERVE|MEM_COMMIT,PAGE_EXECUTE_READWRITE);
    if(!page) return 2;
    memset(page,0x90,4096);
    auto entry=page+64;
    auto target=backward ? page+32 : entry+32;
    const uint8_t yes[]={0xb8,22,0,0,0,0xc3};
    const uint8_t no[]={0xb8,11,0,0,0,0xc3};
    memcpy(target,yes,sizeof yes);
    int offset=cc==16?0:2;
    bool nearCondition=cc>16;
    if(offset) { entry[0]=0x39;entry[1]=0xd1; } // cmp ecx,edx
    int sourceLength=nearCondition?6:2;
    if(nearCondition) {
      entry[offset]=0x0f; entry[offset+1]=(uint8_t)(0x80+cc-17);
      int32_t distance=(int32_t)(target-(entry+offset+6));
      memcpy(entry+offset+2,&distance,4);
    } else {
      entry[offset]=(uint8_t)(cc==16?0xeb:0x70+cc);
      entry[offset+1]=(uint8_t)(target-(entry+offset+2));
    }
    memcpy(entry+offset+sourceLength,no,sizeof no);
    FlushInstructionCache(GetCurrentProcess(),page,4096);
    Fn fn=(Fn)entry;
    int expected[8];
    for(int i=0;i<8;i++) expected[i]=fn(pairs[i][0],pairs[i][1]);
    if(hook(entry,(void*)&replacement,(void**)&original)!=0) return 3;
    auto relocated=(uint8_t*)original+offset;
    int32_t disp;
    int length=cc==16?5:6;
    memcpy(&disp,relocated+(cc==16?1:2),4);
    // Check before execution: the unfixed library jumps into arbitrary memory.
    if(relocated+length+disp!=target) {
      printf("FAIL cc=%d backward=%d branch target delta=%lld\n",cc,backward,
             (long long)((relocated+length+disp)-target));
      failures++;
    } else {
      for(int i=0;i<8;i++) {
        if(original(pairs[i][0],pairs[i][1])!=expected[i] ||
           fn(pairs[i][0],pairs[i][1])!=expected[i]+100) return 4;
      }
      cases++;
    }
    if(destroy(entry)!=0) return 5;
    for(int i=0;i<8;i++) if(fn(pairs[i][0],pairs[i][1])!=expected[i]) return 6;
    VirtualFree(page,0,MEM_RELEASE);
  }
  printf("Branch regression: %d passed, %d failed (16 short + 16 near conditions + short JMP, forward/backward, 8 input pairs).\n",cases,failures);
  return failures?1:0;
}
