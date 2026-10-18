// Diagnostic only: exercise an IL2CPP exception entirely inside native C++.
// No gameplay calls, detours or persistence. The managed caller never receives
// a native exception. Compile with the same /EHs /EHc- /MD as the helpers.
#include <cstdint>
using Image = const void* (*)();
using Factory = void* (*)(const void*,const char*,const char*,const char*);
using Raise = void (*)(void*);
using Callback = int (*)(int);
extern "C" __declspec(dllexport) int dcp_windows_exception_probe(
    Image image, Factory factory, Raise raise, Callback callback) noexcept {
    if (!image || !factory || !raise || !callback) return 1;
    try {
        const void* core = image();
        if (!core) return 2;
        if (callback(1) != 101) return 3;
        void* exception = factory(core,"System","InvalidOperationException","Deception Windows isolated native boundary probe");
        if (!exception) return 4;
        int caught = 0;
        try { raise(exception); return 5; }
        catch (...) { ++caught; }
        if (caught != 1 || callback(2) != 102) return 6;
        // Repeated raising also verifies that the catch restored native unwind
        // state before returning through a second managed callback boundary.
        exception = factory(core,"System","InvalidOperationException","Deception Windows repeated native boundary probe");
        if (!exception) return 7;
        try { raise(exception); return 8; }
        catch (...) { ++caught; }
        return caught == 2 && callback(3) == 103 ? 0 : 9;
    } catch (...) { return 10; }
}
