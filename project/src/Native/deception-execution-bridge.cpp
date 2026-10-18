// DRAFT ONLY: standalone typed native execution boundary. No game lookup,
// detours, managed implementation, or production serializer ABI changes.
// Callbacks MUST contain CLR exceptions and return POD status normally.
#include <cstddef>
#include <cstdint>
#include <type_traits>

#include "deception-platform.h"
#define DCP_EXEC_EXPORT DCP_EXPORT

enum DcpExecPrepareResult : std::int32_t {
    DCP_EXEC_PASS = 0, DCP_EXEC_HANDLED = 1, DCP_EXEC_REJECT = 2
};
enum DcpExecOutcome : std::int32_t {
    DCP_EXEC_COMPLETE = 0,
    DCP_EXEC_PREPARE_REJECTED = 1,
    DCP_EXEC_FINISH_REJECTED = 2,
    DCP_EXEC_ORIGINAL_EXCEPTION = 3,
    DCP_EXEC_CALLBACK_EXCEPTION = 4,
    DCP_EXEC_INVALID_PREPARE = 5
};
enum DcpExecConfigureResult : std::int32_t {
    DCP_EXEC_CONFIGURED = 0, DCP_EXEC_CONFIG_NULL = 1,
    DCP_EXEC_CONFIG_LAYOUT = 2, DCP_EXEC_CONFIG_POINTER = 3,
    DCP_EXEC_CONFIG_EXCEPTION_NAME = 4, DCP_EXEC_CONFIG_ALREADY_SET = 5,
    DCP_EXEC_CONFIG_INTERNAL_ERROR = 6, DCP_EXEC_CONFIG_SITE = 7
};

struct DcpExecCall {
    std::uint32_t struct_size;
    std::uint32_t site; // 0 BuildCommand, 1 BuildAction, 2 TrainCommand, 3 TrainAction.
    void* instance;
    void* state;
    const void* method_info;
};
struct DcpExecPlan {
    std::uint64_t cookie; // Opaque managed identity. Zero is valid.
};
using DcpExecOriginal = void (*)(void*, void*, const void*);
using DcpExecPrepare = std::int32_t (*)(const DcpExecCall*, DcpExecPlan*);
// original_ran is an Int32 exactly 0 or 1, not a marshalled Boolean. Finish runs
// for both PASS and HANDLED, but never after original-native failure.
using DcpExecFinish = std::int32_t (*)(const DcpExecCall*, const DcpExecPlan*, std::int32_t original_ran);
using DcpExecCleanup = std::int32_t (*)(const DcpExecCall*, const DcpExecPlan*, std::int32_t outcome);
using DcpExecFactory = void* (*)(const void*, const char*, const char*, const char*);
using DcpExecRaise = void (*)(void*);
struct DcpExecConfig {
    std::uint32_t struct_size;
    std::uint32_t abi_version;
    DcpExecOriginal original;
    DcpExecPrepare prepare;
    DcpExecFinish finish;
    DcpExecCleanup cleanup;
    DcpExecFactory factory;
    DcpExecRaise raise;
    const void* exception_image;
    const char* exception_namespace; // Copied at configuration: 0..127 bytes.
    const char* exception_name;      // Copied at configuration: 1..127 bytes.
};

static_assert(sizeof(void*) == 8 && sizeof(std::int32_t) == 4);
static_assert(std::is_standard_layout<DcpExecCall>::value && std::is_trivial<DcpExecCall>::value);
static_assert(sizeof(DcpExecCall) == 32 && alignof(DcpExecCall) == 8);
static_assert(offsetof(DcpExecCall, struct_size) == 0 && offsetof(DcpExecCall, site) == 4);
static_assert(offsetof(DcpExecCall, instance) == 8 && offsetof(DcpExecCall, state) == 16 && offsetof(DcpExecCall, method_info) == 24);
static_assert(std::is_standard_layout<DcpExecPlan>::value && std::is_trivial<DcpExecPlan>::value);
static_assert(sizeof(DcpExecPlan) == 8 && alignof(DcpExecPlan) == 8 && offsetof(DcpExecPlan, cookie) == 0);
static_assert(std::is_standard_layout<DcpExecConfig>::value && std::is_trivial<DcpExecConfig>::value);
static_assert(sizeof(DcpExecConfig) == 80 && alignof(DcpExecConfig) == 8);
static_assert(offsetof(DcpExecConfig, struct_size) == 0 && offsetof(DcpExecConfig, abi_version) == 4);
static_assert(offsetof(DcpExecConfig, original) == 8 && offsetof(DcpExecConfig, prepare) == 16);
static_assert(offsetof(DcpExecConfig, finish) == 24 && offsetof(DcpExecConfig, cleanup) == 32);
static_assert(offsetof(DcpExecConfig, factory) == 40 && offsetof(DcpExecConfig, raise) == 48);
static_assert(offsetof(DcpExecConfig, exception_image) == 56 && offsetof(DcpExecConfig, exception_namespace) == 64);
static_assert(offsetof(DcpExecConfig, exception_name) == 72);

DCP_EXEC_EXPORT std::uint32_t dcp_exec_bridge_abi_version() noexcept;
DCP_EXEC_EXPORT std::uint32_t dcp_exec_bridge_call_size() noexcept;
DCP_EXEC_EXPORT std::uint32_t dcp_exec_bridge_plan_size() noexcept;
DCP_EXEC_EXPORT std::uint32_t dcp_exec_bridge_config_size() noexcept;
DCP_EXEC_EXPORT std::uint32_t dcp_exec_bridge_site_count() noexcept;
DCP_EXEC_EXPORT std::int32_t dcp_exec_bridge_configure_site(std::uint32_t, const DcpExecConfig*) noexcept;
DCP_EXEC_EXPORT void dcp_exec_bridge_build_command(void*, void*, const void*);
DCP_EXEC_EXPORT void dcp_exec_bridge_build_action(void*, void*, const void*);
DCP_EXEC_EXPORT void dcp_exec_bridge_train_command(void*, void*, const void*);
DCP_EXEC_EXPORT void dcp_exec_bridge_train_action(void*, void*, const void*);

#ifndef DCP_EXEC_DECLARATIONS_ONLY
#include <atomic>
#include <cstdlib>
#include <cstring>
#include <mutex>

namespace {
constexpr std::uint32_t Abi = 1, SiteCount = 4;
constexpr std::size_t NameCapacity = 128;
struct Site {
    std::atomic<bool> configured{false};
    DcpExecConfig config{};
    char exception_namespace[NameCapacity]{};
    char exception_name[NameCapacity]{};
};
Site Sites[SiteCount];
std::mutex ConfigurationLock;

bool BridgeEntry(DcpExecOriginal original) noexcept {
    return original == dcp_exec_bridge_build_command || original == dcp_exec_bridge_build_action ||
        original == dcp_exec_bridge_train_command || original == dcp_exec_bridge_train_action;
}
bool NameLength(const char* text, bool allow_empty, std::size_t& length) noexcept {
    if (!text) return false;
    for (length = 0; length < NameCapacity; ++length)
        if (text[length] == '\0') return allow_empty || length != 0;
    return false;
}
[[noreturn]] void Reject(const DcpExecConfig& config, const char* message) {
    // Both calls are native and occur after callbacks have returned/unwound.
    // No IL2CPP raiser may be called from a CLR callback implementation.
    void* exception = config.factory(config.exception_image, config.exception_namespace, config.exception_name, message);
    if (!exception) std::abort();
    config.raise(exception);
    std::abort(); // Returning raiser is an invalid supplied ABI, never success.
}
class CleanupOnce final {
    const DcpExecConfig& config;
    const DcpExecCall& call;
    const DcpExecPlan& plan;
    bool completed = false;
public:
    CleanupOnce(const DcpExecConfig& c, const DcpExecCall& x, const DcpExecPlan& p) : config(c), call(x), plan(p) {}
    CleanupOnce(const CleanupOnce&) = delete;
    CleanupOnce& operator=(const CleanupOnce&) = delete;
    std::int32_t Run(std::int32_t outcome) noexcept {
        if (completed) return 0;
        completed = true;
        try { return config.cleanup(&call, &plan, outcome); }
        catch (...) { return -1; } // Must not replace the original native exception.
    }
    ~CleanupOnce() noexcept { Run(DCP_EXEC_CALLBACK_EXCEPTION); }
};
void Invoke(std::uint32_t site_index, void* instance, void* state, const void* method_info) {
    if (site_index >= SiteCount || !Sites[site_index].configured.load(std::memory_order_acquire)) std::abort();
    const auto& config = Sites[site_index].config;
    const DcpExecCall call{sizeof(DcpExecCall), site_index, instance, state, method_info};
    DcpExecPlan plan{};
    CleanupOnce cleanup(config, call, plan);
    std::int32_t prepared;
    try { prepared = config.prepare(&call, &plan); }
    catch (...) {
        // Defensive native-callback-contract guard. This is not permission to
        // let CLR exceptions cross the callback ABI.
        cleanup.Run(DCP_EXEC_CALLBACK_EXCEPTION);
        Reject(config, "Deception execution prepare callback violated its no-throw contract.");
    }
    if (prepared != DCP_EXEC_PASS && prepared != DCP_EXEC_HANDLED) {
        cleanup.Run(prepared == DCP_EXEC_REJECT ? DCP_EXEC_PREPARE_REJECTED : DCP_EXEC_INVALID_PREPARE);
        Reject(config, "Deception execution prepare rejected the operation.");
    }
    const std::int32_t original_ran = prepared == DCP_EXEC_PASS ? 1 : 0;
    if (original_ran) {
        try { config.original(instance, state, method_info); }
        catch (...) {
            cleanup.Run(DCP_EXEC_ORIGINAL_EXCEPTION);
            throw; // Native rethrow after contained cleanup, original identity retained.
        }
    }
    std::int32_t finished;
    try { finished = config.finish(&call, &plan, original_ran); }
    catch (...) {
        cleanup.Run(DCP_EXEC_CALLBACK_EXCEPTION);
        Reject(config, "Deception execution finish callback violated its no-throw contract.");
    }
    if (finished != 0) {
        cleanup.Run(DCP_EXEC_FINISH_REJECTED);
        Reject(config, "Deception execution finish rejected the operation.");
    }
    if (cleanup.Run(DCP_EXEC_COMPLETE) != 0)
        Reject(config, "Deception execution cleanup failed after the operation.");
}
} // namespace

DCP_EXEC_EXPORT std::uint32_t dcp_exec_bridge_abi_version() noexcept { return Abi; }
DCP_EXEC_EXPORT std::uint32_t dcp_exec_bridge_call_size() noexcept { return sizeof(DcpExecCall); }
DCP_EXEC_EXPORT std::uint32_t dcp_exec_bridge_plan_size() noexcept { return sizeof(DcpExecPlan); }
DCP_EXEC_EXPORT std::uint32_t dcp_exec_bridge_config_size() noexcept { return sizeof(DcpExecConfig); }
DCP_EXEC_EXPORT std::uint32_t dcp_exec_bridge_site_count() noexcept { return SiteCount; }
DCP_EXEC_EXPORT std::int32_t dcp_exec_bridge_configure_site(std::uint32_t site_index, const DcpExecConfig* supplied) noexcept {
    try {
        if (site_index >= SiteCount) return DCP_EXEC_CONFIG_SITE;
        std::lock_guard<std::mutex> lock(ConfigurationLock);
        auto& site = Sites[site_index];
        if (site.configured.load(std::memory_order_relaxed)) return DCP_EXEC_CONFIG_ALREADY_SET;
        if (!supplied) return DCP_EXEC_CONFIG_NULL;
        if (supplied->struct_size != sizeof(DcpExecConfig) || supplied->abi_version != Abi) return DCP_EXEC_CONFIG_LAYOUT;
        const auto config = *supplied;
        if (!config.original || BridgeEntry(config.original) || !config.prepare || !config.finish || !config.cleanup ||
            !config.factory || !config.raise || !config.exception_image) return DCP_EXEC_CONFIG_POINTER;
        std::size_t space_length = 0, name_length = 0;
        if (!NameLength(config.exception_namespace, true, space_length) || !NameLength(config.exception_name, false, name_length))
            return DCP_EXEC_CONFIG_EXCEPTION_NAME;
        std::memcpy(site.exception_namespace, config.exception_namespace, space_length + 1);
        std::memcpy(site.exception_name, config.exception_name, name_length + 1);
        site.config = config;
        site.config.exception_namespace = site.exception_namespace;
        site.config.exception_name = site.exception_name;
        site.configured.store(true, std::memory_order_release);
        return DCP_EXEC_CONFIGURED;
    } catch (...) { return DCP_EXEC_CONFIG_INTERNAL_ERROR; }
}
DCP_EXEC_EXPORT void dcp_exec_bridge_build_command(void* self, void* state, const void* method) { Invoke(0, self, state, method); }
DCP_EXEC_EXPORT void dcp_exec_bridge_build_action(void* self, void* state, const void* method) { Invoke(1, self, state, method); }
DCP_EXEC_EXPORT void dcp_exec_bridge_train_command(void* self, void* state, const void* method) { Invoke(2, self, state, method); }
DCP_EXEC_EXPORT void dcp_exec_bridge_train_action(void* self, void* state, const void* method) { Invoke(3, self, state, method); }
#endif
