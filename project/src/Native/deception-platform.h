#ifndef DECEPTION_PLATFORM_H
#define DECEPTION_PLATFORM_H

// Build portability only. Runtime admission still requires an independently
// audited OS/game/helper contract; x64 alone does not verify C++ unwinding.
#if !defined(__x86_64__) && !defined(_M_X64)
#error "Deception native bridges require an audited x86-64 ABI."
#endif

#if defined(_WIN32)
#if defined(DCP_BRIDGE_IMPORT)
#define DCP_EXPORT extern "C" __declspec(dllimport)
#else
#define DCP_EXPORT extern "C" __declspec(dllexport)
#endif
#elif defined(__GNUC__) || defined(__clang__)
#define DCP_EXPORT extern "C" __attribute__((visibility("default")))
#else
#error "Deception native bridge export syntax is not defined for this compiler."
#endif

#endif
