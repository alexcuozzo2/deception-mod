// Own-mod native exception boundary. This file contains no game lookup, detour,
// serializer, or managed runtime implementation. Integration supplies verified
// raw native pointers and keeps all callbacks loaded for the process lifetime.
//
// Define DCP_STATE_BRIDGE_DECLARATIONS_ONLY when including the POD ABI from a
// standalone native harness. The production library exports only dcp_* below.
#include <cstddef>
#include <cstdint>
#include <type_traits>

#include "deception-platform.h"

enum DcpStateOperation : std::int32_t { DCP_SERIALIZE = 0, DCP_DESERIALIZE = 1 };
enum DcpPrepareResult : std::int32_t {
    DCP_PASS = 0, DCP_REPLACE_STREAM = 1, DCP_REJECT = 2
};
enum DcpCleanupOutcome : std::int32_t {
    DCP_COMPLETE = 0,
    DCP_PREPARE_REJECTED = 1,
    DCP_INVALID_PLAN = 2,
    DCP_FINISH_REJECTED = 3,
    DCP_ORIGINAL_EXCEPTION = 4,
    DCP_CALLBACK_EXCEPTION = 5
};
enum DcpConfigureResult : std::int32_t {
    DCP_CONFIGURED = 0,
    DCP_CONFIG_NULL = 1,
    DCP_CONFIG_LAYOUT = 2,
    DCP_CONFIG_POINTER = 3,
    DCP_CONFIG_EXCEPTION_NAME = 4,
    DCP_CONFIG_ALREADY_SET = 5,
    DCP_CONFIG_INTERNAL_ERROR = 6,
    DCP_CONFIG_SITE = 7
};

struct DcpStateCall {
    std::uint32_t struct_size;
    std::int32_t operation;
    std::int32_t version;
    std::uint32_t site; // 0=GameState, 1=BuildCommand, 2=BuildAction, 3=TrainCommand, 4=TrainAction. No packed/Boolean fields.
    void* instance;
    void* original_stream;
    const void* method_info;
};

struct DcpStatePlan {
    std::uint64_t cookie; // Opaque managed operation identity; zero is valid.
    void* replacement_stream;
};

using DcpOriginal = void (*)(void*, void*, std::int32_t, const void*);
// Callbacks must catch ALL CLR exceptions and return normally. They must never
// raise a native exception. Return zero from finish/cleanup for success.
using DcpPrepare = std::int32_t (*)(const DcpStateCall*, DcpStatePlan*);
using DcpFinish = std::int32_t (*)(const DcpStateCall*, const DcpStatePlan*);
using DcpCleanup = std::int32_t (*)(const DcpStateCall*, const DcpStatePlan*, std::int32_t);
// Exact il2cpp_exception_from_name_msg / il2cpp_raise_exception shapes.
using DcpExceptionFactory = void* (*)(const void*, const char*, const char*, const char*);
using DcpRaiseException = void (*)(void*);

struct DcpStateBridgeConfig {
    std::uint32_t struct_size;
    std::uint32_t abi_version;
    DcpOriginal original_serialize;
    DcpOriginal original_deserialize;
    DcpPrepare prepare;
    DcpFinish finish;
    DcpCleanup cleanup;
    DcpExceptionFactory exception_factory;
    DcpRaiseException raise_exception;
    const void* exception_image;
    const char* exception_namespace; // Copied at configure, 0..127 bytes.
    const char* exception_name;      // Copied at configure, 1..127 bytes.
};

static_assert(sizeof(void*) == 8 && sizeof(std::int32_t) == 4);
static_assert(std::is_standard_layout<DcpStateCall>::value && std::is_trivial<DcpStateCall>::value);
static_assert(sizeof(DcpStateCall) == 40 && alignof(DcpStateCall) == 8);
static_assert(offsetof(DcpStateCall, struct_size) == 0 && offsetof(DcpStateCall, operation) == 4);
static_assert(offsetof(DcpStateCall, version) == 8 && offsetof(DcpStateCall, site) == 12);
static_assert(offsetof(DcpStateCall, instance) == 16 && offsetof(DcpStateCall, original_stream) == 24);
static_assert(offsetof(DcpStateCall, method_info) == 32);
static_assert(std::is_standard_layout<DcpStatePlan>::value && std::is_trivial<DcpStatePlan>::value);
static_assert(sizeof(DcpStatePlan) == 16 && alignof(DcpStatePlan) == 8);
static_assert(offsetof(DcpStatePlan, cookie) == 0 && offsetof(DcpStatePlan, replacement_stream) == 8);
static_assert(std::is_standard_layout<DcpStateBridgeConfig>::value && std::is_trivial<DcpStateBridgeConfig>::value);
static_assert(sizeof(DcpStateBridgeConfig) == 88 && alignof(DcpStateBridgeConfig) == 8);
static_assert(offsetof(DcpStateBridgeConfig, struct_size) == 0 && offsetof(DcpStateBridgeConfig, abi_version) == 4);
static_assert(offsetof(DcpStateBridgeConfig, original_serialize) == 8 && offsetof(DcpStateBridgeConfig, original_deserialize) == 16);
static_assert(offsetof(DcpStateBridgeConfig, prepare) == 24 && offsetof(DcpStateBridgeConfig, finish) == 32);
static_assert(offsetof(DcpStateBridgeConfig, cleanup) == 40 && offsetof(DcpStateBridgeConfig, exception_factory) == 48);
static_assert(offsetof(DcpStateBridgeConfig, raise_exception) == 56 && offsetof(DcpStateBridgeConfig, exception_image) == 64);
static_assert(offsetof(DcpStateBridgeConfig, exception_namespace) == 72 && offsetof(DcpStateBridgeConfig, exception_name) == 80);

DCP_EXPORT std::uint32_t dcp_state_bridge_abi_version() noexcept;
DCP_EXPORT std::uint32_t dcp_state_bridge_site_count() noexcept;
DCP_EXPORT std::uint32_t dcp_state_bridge_config_size() noexcept;
DCP_EXPORT std::uint32_t dcp_state_bridge_call_size() noexcept;
DCP_EXPORT std::uint32_t dcp_state_bridge_plan_size() noexcept;
DCP_EXPORT std::int32_t dcp_state_bridge_configure(const DcpStateBridgeConfig*) noexcept;
DCP_EXPORT std::int32_t dcp_state_bridge_configure_site(std::uint32_t, const DcpStateBridgeConfig*) noexcept;
DCP_EXPORT void dcp_state_bridge_serialize(void*, void*, std::int32_t, const void*);
DCP_EXPORT void dcp_state_bridge_deserialize(void*, void*, std::int32_t, const void*);
DCP_EXPORT void dcp_state_bridge_command_serialize(void*, void*, std::int32_t, const void*);
DCP_EXPORT void dcp_state_bridge_command_deserialize(void*, void*, std::int32_t, const void*);
DCP_EXPORT void dcp_state_bridge_action_serialize(void*, void*, std::int32_t, const void*);
DCP_EXPORT void dcp_state_bridge_action_deserialize(void*, void*, std::int32_t, const void*);
DCP_EXPORT void dcp_state_bridge_train_command_serialize(void*, void*, std::int32_t, const void*);
DCP_EXPORT void dcp_state_bridge_train_command_deserialize(void*, void*, std::int32_t, const void*);
DCP_EXPORT void dcp_state_bridge_train_action_serialize(void*, void*, std::int32_t, const void*);
DCP_EXPORT void dcp_state_bridge_train_action_deserialize(void*, void*, std::int32_t, const void*);

#ifndef DCP_STATE_BRIDGE_DECLARATIONS_ONLY
#include <atomic>
#include <cstdlib>
#include <cstring>
#include <mutex>

namespace {
constexpr std::uint32_t AbiVersion = 3;
constexpr std::uint32_t SiteCount = 5;
constexpr std::size_t NameCapacity = 128;
std::mutex ConfigurationLock;
struct Site {
    std::atomic<bool> configured{false};
    DcpStateBridgeConfig configuration{};
    char exception_namespace[NameCapacity]{};
    char exception_name[NameCapacity]{};
};
Site Sites[SiteCount];
bool BridgeEntry(DcpOriginal value) noexcept {
    return value == dcp_state_bridge_serialize || value == dcp_state_bridge_deserialize ||
        value == dcp_state_bridge_command_serialize || value == dcp_state_bridge_command_deserialize ||
        value == dcp_state_bridge_action_serialize || value == dcp_state_bridge_action_deserialize ||
        value == dcp_state_bridge_train_command_serialize || value == dcp_state_bridge_train_command_deserialize ||
        value == dcp_state_bridge_train_action_serialize || value == dcp_state_bridge_train_action_deserialize;
}

bool BoundedName(const char* value, bool allow_empty, std::size_t& length) noexcept {
    if (!value) return false;
    for (length = 0; length < NameCapacity; ++length) {
        if (value[length] == '\0') return allow_empty || length != 0;
    }
    return false;
}

// Factory and raiser are raw native functions, called with no callback active.
// A null exception or a returning raiser is an ABI/integration failure. Never
// return apparent serializer success in that condition.
[[noreturn]] void Reject(const DcpStateBridgeConfig& config, const char* message) {
    void* exception = config.exception_factory(config.exception_image,
        config.exception_namespace, config.exception_name, message);
    if (!exception) std::abort();
    config.raise_exception(exception);
    std::abort();
}

class CleanupOnce final {
    const DcpStateBridgeConfig& config;
    const DcpStateCall& call;
    const DcpStatePlan& plan;
    bool completed = false;
public:
    CleanupOnce(const DcpStateBridgeConfig& c, const DcpStateCall& x, const DcpStatePlan& p)
        : config(c), call(x), plan(p) {}
    CleanupOnce(const CleanupOnce&) = delete;
    CleanupOnce& operator=(const CleanupOnce&) = delete;
    std::int32_t Run(std::int32_t outcome) noexcept {
        if (completed) return 0;
        completed = true; // Mark BEFORE invoking cleanup, including exceptions.
        try { return config.cleanup(&call, &plan, outcome); }
        catch (...) { return -1; } // Contract violation must not mask an original exception.
    }
    ~CleanupOnce() noexcept { Run(DCP_CALLBACK_EXCEPTION); }
};

void Invoke(std::uint32_t site_index, std::int32_t operation, void* instance, void* stream,
            std::int32_t version, const void* method_info) {
    // configure must finish before either detour is applied. An entry before
    // configuration cannot safely delegate or construct a native exception.
    if (site_index >= SiteCount) std::abort();
    const auto& site = Sites[site_index];
    if (!site.configured.load(std::memory_order_acquire)) std::abort();
    const auto& config = site.configuration; // Immutable for process lifetime.
    const DcpStateCall call{sizeof(DcpStateCall), operation, version, site_index,
                           instance, stream, method_info};
    DcpStatePlan plan{};
    CleanupOnce cleanup(config, call, plan);
    const auto prepared = config.prepare(&call, &plan);
    if (prepared != DCP_PASS && prepared != DCP_REPLACE_STREAM) {
        cleanup.Run(DCP_PREPARE_REJECTED);
        Reject(config, "Deception state bridge prepare rejected the operation.");
    }
    if ((prepared == DCP_PASS && plan.replacement_stream != nullptr) ||
        (prepared == DCP_REPLACE_STREAM && plan.replacement_stream == nullptr)) {
        cleanup.Run(DCP_INVALID_PLAN);
        Reject(config, "Deception state bridge received an invalid replacement-stream plan.");
    }

    const DcpOriginal original = operation == DCP_SERIALIZE
        ? config.original_serialize : config.original_deserialize;
    try {
        // Preserve receiver, signed version and hidden method-info exactly.
        // This pointer must be the provider's raw, unapplied-original trampoline.
        original(instance, prepared == DCP_PASS ? stream : plan.replacement_stream,
                 version, method_info);
    } catch (...) {
        cleanup.Run(DCP_ORIGINAL_EXCEPTION);
        throw; // Rethrow only after the no-throw cleanup callback has returned.
    }

    if (config.finish(&call, &plan) != 0) {
        cleanup.Run(DCP_FINISH_REJECTED);
        Reject(config, "Deception state bridge finish rejected the operation.");
    }
    if (cleanup.Run(DCP_COMPLETE) != 0)
        Reject(config, "Deception state bridge cleanup failed after the operation.");
}
} // namespace

DCP_EXPORT std::uint32_t dcp_state_bridge_abi_version() noexcept { return AbiVersion; }
DCP_EXPORT std::uint32_t dcp_state_bridge_site_count() noexcept { return SiteCount; }
DCP_EXPORT std::uint32_t dcp_state_bridge_config_size() noexcept { return sizeof(DcpStateBridgeConfig); }
DCP_EXPORT std::uint32_t dcp_state_bridge_call_size() noexcept { return sizeof(DcpStateCall); }
DCP_EXPORT std::uint32_t dcp_state_bridge_plan_size() noexcept { return sizeof(DcpStatePlan); }

DCP_EXPORT std::int32_t dcp_state_bridge_configure(const DcpStateBridgeConfig* supplied) noexcept {
    return dcp_state_bridge_configure_site(0, supplied);
}
DCP_EXPORT std::int32_t dcp_state_bridge_configure_site(std::uint32_t site_index, const DcpStateBridgeConfig* supplied) noexcept {
    try {
        if (site_index >= SiteCount) return DCP_CONFIG_SITE;
        std::lock_guard<std::mutex> lock(ConfigurationLock);
        auto& site = Sites[site_index];
        if (site.configured.load(std::memory_order_relaxed)) return DCP_CONFIG_ALREADY_SET;
        if (!supplied) return DCP_CONFIG_NULL;
        if (supplied->struct_size != sizeof(DcpStateBridgeConfig) || supplied->abi_version != AbiVersion)
            return DCP_CONFIG_LAYOUT;
        const auto config = *supplied;
        if (!config.original_serialize || !config.original_deserialize || !config.prepare ||
            !config.finish || !config.cleanup || !config.exception_factory || !config.raise_exception ||
            !config.exception_image ||
            BridgeEntry(config.original_serialize) || BridgeEntry(config.original_deserialize))
            return DCP_CONFIG_POINTER;
        std::size_t namespace_length = 0, name_length = 0;
        if (!BoundedName(config.exception_namespace, true, namespace_length) ||
            !BoundedName(config.exception_name, false, name_length))
            return DCP_CONFIG_EXCEPTION_NAME;
        std::memcpy(site.exception_namespace, config.exception_namespace, namespace_length + 1);
        std::memcpy(site.exception_name, config.exception_name, name_length + 1);
        site.configuration = config;
        site.configuration.exception_namespace = site.exception_namespace;
        site.configuration.exception_name = site.exception_name;
        site.configured.store(true, std::memory_order_release);
        return DCP_CONFIGURED;
    } catch (...) { return DCP_CONFIG_INTERNAL_ERROR; }
}

DCP_EXPORT void dcp_state_bridge_serialize(void* instance, void* stream,
                                          std::int32_t version, const void* method_info) {
    Invoke(0, DCP_SERIALIZE, instance, stream, version, method_info);
}
DCP_EXPORT void dcp_state_bridge_deserialize(void* instance, void* stream,
                                            std::int32_t version, const void* method_info) {
    Invoke(0, DCP_DESERIALIZE, instance, stream, version, method_info);
}
DCP_EXPORT void dcp_state_bridge_command_serialize(void* instance, void* stream,
    std::int32_t version, const void* method_info) {
    Invoke(1, DCP_SERIALIZE, instance, stream, version, method_info);
}
DCP_EXPORT void dcp_state_bridge_command_deserialize(void* instance, void* stream,
    std::int32_t version, const void* method_info) {
    Invoke(1, DCP_DESERIALIZE, instance, stream, version, method_info);
}
DCP_EXPORT void dcp_state_bridge_action_serialize(void* instance, void* stream,
    std::int32_t version, const void* method_info) {
    Invoke(2, DCP_SERIALIZE, instance, stream, version, method_info);
}
DCP_EXPORT void dcp_state_bridge_action_deserialize(void* instance, void* stream,
    std::int32_t version, const void* method_info) {
    Invoke(2, DCP_DESERIALIZE, instance, stream, version, method_info);
}
DCP_EXPORT void dcp_state_bridge_train_command_serialize(void* instance, void* stream,
    std::int32_t version, const void* method_info) {
    Invoke(3, DCP_SERIALIZE, instance, stream, version, method_info);
}
DCP_EXPORT void dcp_state_bridge_train_command_deserialize(void* instance, void* stream,
    std::int32_t version, const void* method_info) {
    Invoke(3, DCP_DESERIALIZE, instance, stream, version, method_info);
}
DCP_EXPORT void dcp_state_bridge_train_action_serialize(void* instance, void* stream,
    std::int32_t version, const void* method_info) {
    Invoke(4, DCP_SERIALIZE, instance, stream, version, method_info);
}
DCP_EXPORT void dcp_state_bridge_train_action_deserialize(void* instance, void* stream,
    std::int32_t version, const void* method_info) {
    Invoke(4, DCP_DESERIALIZE, instance, stream, version, method_info);
}

#endif
