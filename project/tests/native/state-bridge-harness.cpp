// Standalone C++ control-flow tests only. No IL2CPP, CLR, game, or detour calls.
#define DCP_STATE_BRIDGE_DECLARATIONS_ONLY
#include "../../src/Native/deception-state-bridge.cpp"
#include <atomic>
#include <csignal>
#include <cstdio>
#include <cstring>
#include <functional>
#include <iterator>
#include <limits>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>
#if defined(_WIN32)
#include <windows.h>
#include <cstdlib>
#else
#include <sys/resource.h>
#include <sys/wait.h>
#include <unistd.h>
#endif

namespace {
struct OriginalError { int identity; };
struct CallbackError { int identity; };
struct FactoryError {};
struct ValidationError { std::string message; };
void Require(bool value, const char* detail) { if (!value) throw std::runtime_error(detail); }

struct Scenario {
    std::uint32_t site = 0;
    int operation = DCP_SERIALIZE;
    std::int32_t version = 123;
    std::uint64_t cookie = 0xFEDCBA9876543210ULL;
    int instance_storage = 1, stream_storage = 2, replacement_storage = 3, method_storage = 4;
    int prepare_result = DCP_PASS, finish_result = 0, cleanup_result = 0;
    int expected_outcome = DCP_COMPLETE;
    bool provide_replacement = false, original_throws = false;
    bool prepare_throws = false, finish_throws = false, cleanup_throws = false;
    bool factory_null = false, factory_throws = false, raiser_returns = false;
    int prepares = 0, originals = 0, finishes = 0, cleanups = 0, factories = 0, raises = 0;
    int callback_depth = 0;
    std::string failure, message;
    std::vector<std::string> order;
    Scenario* nested = nullptr;
    void Check(bool value, const char* detail) { if (!value && failure.empty()) failure = detail; }
    void CheckCall(const DcpStateCall* call) {
        Check(call->struct_size == 40 && call->site == site, "call layout/site");
        Check(call->operation == operation && call->version == version, "operation/signed version");
        Check(call->instance == &instance_storage, "receiver identity");
        Check(call->original_stream == &stream_storage, "original stream identity");
        Check(call->method_info == &method_storage, "hidden method-info identity");
    }
    void Verify() {
        Require(failure.empty(), failure.c_str());
        Require(callback_depth == 0, "callback remains active");
        Require(prepares == 1 && cleanups == 1, "prepare/cleanup not exactly once");
    }
};
thread_local Scenario* Current = nullptr;
struct ScenarioScope {
    Scenario* previous;
    explicit ScenarioScope(Scenario& scenario) : previous(Current) { Current = &scenario; }
    ~ScenarioScope() { Current = previous; }
};
struct CallbackScope {
    Scenario& scenario;
    explicit CallbackScope(Scenario& x) : scenario(x) { ++scenario.callback_depth; }
    ~CallbackScope() { --scenario.callback_depth; }
};

const DcpOriginal Entries[5][2] = {
    {dcp_state_bridge_serialize, dcp_state_bridge_deserialize},
    {dcp_state_bridge_command_serialize, dcp_state_bridge_command_deserialize},
    {dcp_state_bridge_action_serialize, dcp_state_bridge_action_deserialize},
    {dcp_state_bridge_train_command_serialize, dcp_state_bridge_train_command_deserialize},
    {dcp_state_bridge_train_action_serialize, dcp_state_bridge_train_action_deserialize}
};
void Invoke(Scenario& scenario) {
    ScenarioScope scope(scenario);
    Require(scenario.site < 5 && (scenario.operation == DCP_SERIALIZE || scenario.operation == DCP_DESERIALIZE),
            "invalid harness entry selector");
    const auto bridge = Entries[scenario.site][scenario.operation];
    bridge(&scenario.instance_storage, &scenario.stream_storage, scenario.version, &scenario.method_storage);
}

std::int32_t Prepare(const DcpStateCall* call, DcpStatePlan* plan) {
    auto& s = *Current; CallbackScope frame(s);
    ++s.prepares; s.order.push_back("prepare"); s.CheckCall(call);
    s.Check(plan->cookie == 0 && plan->replacement_stream == nullptr, "plan not initialized");
    plan->cookie = s.cookie;
    plan->replacement_stream = s.provide_replacement ? &s.replacement_storage : nullptr;
    if (s.prepare_throws) throw CallbackError{17};
    return s.prepare_result;
}
std::int32_t Finish(const DcpStateCall* call, const DcpStatePlan* plan) {
    auto& s = *Current; CallbackScope frame(s);
    ++s.finishes; s.order.push_back("finish"); s.CheckCall(call);
    s.Check(plan->cookie == s.cookie, "finish cookie changed");
    if (s.finish_throws) throw CallbackError{19};
    return s.finish_result;
}
std::int32_t Cleanup(const DcpStateCall* call, const DcpStatePlan* plan, std::int32_t outcome) {
    auto& s = *Current; CallbackScope frame(s);
    ++s.cleanups; s.order.push_back("cleanup"); s.CheckCall(call);
    s.Check(plan->cookie == s.cookie, "cleanup cookie changed");
    s.Check(outcome == s.expected_outcome, "cleanup outcome changed");
    if (s.cleanup_throws) throw CallbackError{23};
    return s.cleanup_result;
}

void Original(std::uint32_t site, int operation, void* instance, void* stream, std::int32_t version, const void* method_info) {
    auto& s = *Current;
    ++s.originals; s.order.push_back("original");
    s.Check(s.callback_depth == 0, "original entered inside callback");
    s.Check(site == s.site && operation == s.operation, "wrong raw original site/direction selected");
    s.Check(instance == &s.instance_storage && version == s.version, "raw receiver/version changed");
    s.Check(method_info == &s.method_storage, "raw hidden method-info changed");
    s.Check(stream == (s.prepare_result == DCP_PASS ? &s.stream_storage : &s.replacement_storage),
            "wrong native stream");
    if (s.nested) Invoke(*s.nested);
    if (s.original_throws) throw OriginalError{41};
}
void Serialize(void* instance, void* stream, std::int32_t version, const void* method_info) {
    Original(0, DCP_SERIALIZE, instance, stream, version, method_info);
}
void Deserialize(void* instance, void* stream, std::int32_t version, const void* method_info) {
    Original(0, DCP_DESERIALIZE, instance, stream, version, method_info);
}
int ImageStorage[5];
const char* const Namespaces[5] = {"System", "CommandScope", "ActionScope", "TrainCommandScope", "TrainActionScope"};
const char* const ExceptionNames[5] = {"InvalidOperationException", "CommandException", "ActionException", "TrainCommandException", "TrainActionException"};
void* Factory(const void* image, const char* namespc, const char* name, const char* message) {
    auto& s = *Current;
    ++s.factories; s.order.push_back("factory");
    s.Check(s.callback_depth == 0, "exception factory called inside callback");
    s.Check(s.cleanups == 1, "exception factory called before cleanup");
    s.Check(image == &ImageStorage[s.site], "exception image changed or crossed sites");
    s.Check(std::strcmp(namespc, Namespaces[s.site]) == 0 && std::strcmp(name, ExceptionNames[s.site]) == 0,
            "configured names were not copied exactly");
    s.message = message;
    if (s.factory_throws) throw FactoryError{};
    return s.factory_null ? nullptr : &s;
}
void Raise(void* exception) {
    auto& s = *Current;
    ++s.raises; s.order.push_back("raise");
    s.Check(exception == &s && s.callback_depth == 0 && s.cleanups == 1,
            "raise pointer/order/callback violation");
    if (!s.raiser_returns) throw ValidationError{s.message};
}

// Distinct addresses for every site's original/callback/factory/raise binding.
// The shared implementation still verifies the actual site field and cookie.
template<std::uint32_t Site> void SiteSerialize(void* self, void* stream, std::int32_t version, const void* method) {
    Original(Site, DCP_SERIALIZE, self, stream, version, method);
}
template<std::uint32_t Site> void SiteDeserialize(void* self, void* stream, std::int32_t version, const void* method) {
    Original(Site, DCP_DESERIALIZE, self, stream, version, method);
}
template<std::uint32_t Site> std::int32_t SitePrepare(const DcpStateCall* call, DcpStatePlan* plan) {
    Current->Check(Current->site == Site, "wrong configured prepare callback"); return Prepare(call, plan);
}
template<std::uint32_t Site> std::int32_t SiteFinish(const DcpStateCall* call, const DcpStatePlan* plan) {
    Current->Check(Current->site == Site, "wrong configured finish callback"); return Finish(call, plan);
}
template<std::uint32_t Site> std::int32_t SiteCleanup(const DcpStateCall* call, const DcpStatePlan* plan, std::int32_t outcome) {
    Current->Check(Current->site == Site, "wrong configured cleanup callback"); return Cleanup(call, plan, outcome);
}
template<std::uint32_t Site> void* SiteFactory(const void* image, const char* namespc, const char* name, const char* message) {
    Current->Check(Current->site == Site, "wrong configured exception factory"); return Factory(image, namespc, name, message);
}
template<std::uint32_t Site> void SiteRaise(void* exception) {
    Current->Check(Current->site == Site, "wrong configured native raiser"); Raise(exception);
}

DcpStateBridgeConfig SiteConfig(std::uint32_t site) {
    if (site == 1) return {88, 3, SiteSerialize<1>, SiteDeserialize<1>, SitePrepare<1>, SiteFinish<1>,
        SiteCleanup<1>, SiteFactory<1>, SiteRaise<1>, &ImageStorage[1], Namespaces[1], ExceptionNames[1]};
    if (site == 2) return {88, 3, SiteSerialize<2>, SiteDeserialize<2>, SitePrepare<2>, SiteFinish<2>,
        SiteCleanup<2>, SiteFactory<2>, SiteRaise<2>, &ImageStorage[2], Namespaces[2], ExceptionNames[2]};
    if (site == 3) return {88, 3, SiteSerialize<3>, SiteDeserialize<3>, SitePrepare<3>, SiteFinish<3>,
        SiteCleanup<3>, SiteFactory<3>, SiteRaise<3>, &ImageStorage[3], Namespaces[3], ExceptionNames[3]};
    if (site == 4) return {88, 3, SiteSerialize<4>, SiteDeserialize<4>, SitePrepare<4>, SiteFinish<4>,
        SiteCleanup<4>, SiteFactory<4>, SiteRaise<4>, &ImageStorage[4], Namespaces[4], ExceptionNames[4]};
    return {88, 3, Serialize, Deserialize, SitePrepare<0>, SiteFinish<0>, SiteCleanup<0>, SiteFactory<0>,
        SiteRaise<0>, &ImageStorage[0], Namespaces[0], ExceptionNames[0]};
}

void RejectBridgeOriginals(std::uint32_t site, const DcpStateBridgeConfig& config) {
    for (const auto& pair : Entries) for (auto entry : pair) {
        auto bad = config; bad.original_serialize = entry;
        Require(dcp_state_bridge_configure_site(site, &bad) == DCP_CONFIG_POINTER,
                "bridge serialize original permits a same-site or cross-site cycle");
        bad = config; bad.original_deserialize = entry;
        Require(dcp_state_bridge_configure_site(site, &bad) == DCP_CONFIG_POINTER,
                "bridge deserialize original permits a same-site or cross-site cycle");
    }
}

void ExpectValidation(Scenario& s, const char* stage) {
    bool caught = false;
    try { Invoke(s); }
    catch (const ValidationError& error) {
        caught = true;
        Require(error.message.find(stage) != std::string::npos, "wrong native rejection stage");
    }
    Require(caught, "rejected operation returned apparent success");
    s.Verify();
    Require(s.factories == 1 && s.raises == 1, "native rejection not exactly once");
}

#if defined(_WIN32)
// Windows has no fork. A fresh invocation replays the deterministic setup up
// to the requested abort probe; earlier probes are skipped in that child.
int AbortProbe = 0, RequestedAbortProbe = 0;
std::wstring Executable;
#endif
void ExpectAbort(const std::function<void()>& action) {
    std::fflush(nullptr);
#if defined(_WIN32)
    const int probe = ++AbortProbe;
    if (RequestedAbortProbe != 0) {
        if (probe != RequestedAbortProbe) return;
        SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
        _set_abort_behavior(0, _WRITE_ABORT_MSG | _CALL_REPORTFAULT);
        std::signal(SIGABRT, [](int) { std::_Exit(77); });
        action(); std::_Exit(99);
    }
    std::wstring command = L"\"" + Executable + L"\" --abort-probe " + std::to_wstring(probe);
    STARTUPINFOW startup{}; startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    Require(CreateProcessW(Executable.c_str(), command.data(), nullptr, nullptr, FALSE,
                          CREATE_NO_WINDOW, nullptr, nullptr, &startup, &process) != FALSE,
            "CreateProcessW failed");
    const DWORD waited = WaitForSingleObject(process.hProcess, 60000);
    DWORD status = 0;
    const bool completed = waited == WAIT_OBJECT_0 && GetExitCodeProcess(process.hProcess, &status);
    if (!completed) { TerminateProcess(process.hProcess, 98); WaitForSingleObject(process.hProcess, 5000); }
    CloseHandle(process.hThread); CloseHandle(process.hProcess);
    Require(completed && status == 77, "must request abort instead of apparent success");
#else
    const pid_t child = fork();
    Require(child >= 0, "fork failed");
    if (child == 0) {
        const rlimit no_core{0, 0}; setrlimit(RLIMIT_CORE, &no_core);
        // Exercise the production abort branch without asking macOS to produce
        // a crash report for an intentional mock failure. _exit is signal-safe.
        std::signal(SIGABRT, [](int) { _exit(77); });
        action(); _exit(99);
    }
    int status = 0;
    Require(waitpid(child, &status, 0) == child, "waitpid failed");
    Require(WIFEXITED(status) && WEXITSTATUS(status) == 77, "must request abort instead of apparent success");
#endif
}

int Passed = 0, Failed = 0;
void Case(const char* name, const std::function<void()>& run) {
    try { run(); ++Passed; std::printf("PASS %s\n", name); }
    catch (const std::exception& error) { ++Failed; std::printf("FAIL %s: %s\n", name, error.what()); }
    catch (...) { ++Failed; std::printf("FAIL %s: unexpected exception\n", name); }
}
} // namespace

int main(int argc, char** argv) {
#if defined(_WIN32)
    wchar_t path[32768];
    const DWORD length = GetModuleFileNameW(nullptr, path, static_cast<DWORD>(std::size(path)));
    if (length == 0 || length >= std::size(path)) return 2;
    Executable.assign(path, length);
    if (argc == 3 && std::strcmp(argv[1], "--abort-probe") == 0) {
        char* end = nullptr; const long probe = std::strtol(argv[2], &end, 10);
        if (!end || *end || probe < 1 || probe > 100) return 2;
        RequestedAbortProbe = static_cast<int>(probe);
    } else if (argc != 1) return 2;
#else
    (void)argv; if (argc != 1) return 2;
#endif
    std::puts("Standalone x86-64 C++ bridge control flow. No IL2CPP/CLR/game/detours.");
    Case("ABI sizes and version", [] {
        Require(dcp_state_bridge_abi_version() == 3 && dcp_state_bridge_site_count() == 5 && dcp_state_bridge_config_size() == 88 &&
                dcp_state_bridge_call_size() == 40 && dcp_state_bridge_plan_size() == 16, "ABI mismatch");
    });
    Case("unconfigured sites cannot return success in either direction", [] {
        for (std::uint32_t site = 0; site < 5; ++site) for (int operation : {DCP_SERIALIZE, DCP_DESERIALIZE})
            ExpectAbort([=] { Scenario s; s.site = site; s.operation = operation; Invoke(s); });
    });
    Case("invalid site rejects without touching any configuration", [] {
        auto config = SiteConfig(0);
        for (auto site : {5U, std::numeric_limits<std::uint32_t>::max()}) {
            Require(dcp_state_bridge_configure_site(site, &config) == DCP_CONFIG_SITE, "invalid site accepted");
            Require(dcp_state_bridge_configure_site(site, nullptr) == DCP_CONFIG_SITE, "invalid site dereferenced config");
        }
    });

    char namespc[] = "System", name[] = "InvalidOperationException";
    auto config = SiteConfig(0); config.exception_namespace = namespc; config.exception_name = name;
    Case("invalid configuration is nonthrowing and recoverable", [&] {
        Require(dcp_state_bridge_configure(nullptr) == DCP_CONFIG_NULL, "null config");
        auto bad = config; bad.struct_size = 87;
        Require(dcp_state_bridge_configure(&bad) == DCP_CONFIG_LAYOUT, "wrong size accepted");
        bad = config; bad.abi_version = 2;
        Require(dcp_state_bridge_configure(&bad) == DCP_CONFIG_LAYOUT, "wrong ABI accepted");
        bad = config; bad.cleanup = nullptr;
        Require(dcp_state_bridge_configure(&bad) == DCP_CONFIG_POINTER, "missing callback accepted");
        bad = config; bad.original_serialize = dcp_state_bridge_deserialize;
        Require(dcp_state_bridge_configure(&bad) == DCP_CONFIG_POINTER, "recursive original accepted");
        RejectBridgeOriginals(0, config);
        bad = config; bad.exception_name = "";
        Require(dcp_state_bridge_configure(&bad) == DCP_CONFIG_EXCEPTION_NAME, "empty type accepted");
        char too_long[128]; std::memset(too_long, 'x', sizeof(too_long));
        bad = config; bad.exception_namespace = too_long;
        Require(dcp_state_bridge_configure(&bad) == DCP_CONFIG_EXCEPTION_NAME, "unbounded namespace accepted");
        Require(dcp_state_bridge_configure(&config) == DCP_CONFIGURED, "valid configuration failed");
        // Demonstrate that mutable caller strings are no longer borrowed.
        namespc[0] = 'X'; name[0] = 'X';
        Require(dcp_state_bridge_configure(&config) == DCP_CONFIG_ALREADY_SET, "configuration replaced");
        Require(dcp_state_bridge_configure_site(0, &config) == DCP_CONFIG_ALREADY_SET, "site0 alias config diverged");
    });
    for (std::uint32_t site : {1U, 2U, 3U, 4U}) {
        const std::string title = "independent immutable configuration site " + std::to_string(site);
        Case(title.c_str(), [=] {
            Scenario unchanged; Invoke(unchanged); unchanged.Verify();
            ExpectAbort([=] { Scenario missing; missing.site = site; Invoke(missing); });
            auto next = SiteConfig(site);
            Require(dcp_state_bridge_configure_site(site, nullptr) == DCP_CONFIG_NULL, "other site mistaken for configured");
            RejectBridgeOriginals(site, next);
            Scenario still_unchanged; Invoke(still_unchanged); still_unchanged.Verify();
            char ns[64], type[64]; std::strcpy(ns, Namespaces[site]); std::strcpy(type, ExceptionNames[site]);
            next.exception_namespace = ns; next.exception_name = type;
            Require(dcp_state_bridge_configure_site(site, &next) == DCP_CONFIGURED, "independent configuration failed");
            ns[0] = 'X'; type[0] = 'X';
            auto other = SiteConfig(0);
            Require(dcp_state_bridge_configure_site(site, &other) == DCP_CONFIG_ALREADY_SET, "site configuration replaced");
            Require(dcp_state_bridge_configure(nullptr) == DCP_CONFIG_ALREADY_SET, "secondary configure damaged site0");
        });
    }
    for (std::uint32_t site = 0; site < 5; ++site) for (int operation : {DCP_SERIALIZE, DCP_DESERIALIZE}) {
        const std::string label = "site " + std::to_string(site) + (operation == DCP_SERIALIZE ? " serialize" : " deserialize");
        Case((label + " passthrough signed boundary").c_str(), [=] {
            Scenario s; s.site = site; s.operation = operation; s.cookie = 0;
            s.version = operation == DCP_SERIALIZE ? std::numeric_limits<std::int32_t>::min()
                                                   : std::numeric_limits<std::int32_t>::max();
            Invoke(s); s.Verify();
            Require(s.originals == 1 && s.finishes == 1 && s.factories == 0, "passthrough lifecycle");
            Require(s.order == std::vector<std::string>{"prepare", "original", "finish", "cleanup"}, "passthrough order");
        });
        Case((label + " replacement").c_str(), [=] {
            Scenario s; s.site = site; s.operation = operation; s.prepare_result = DCP_REPLACE_STREAM; s.provide_replacement = true;
            Invoke(s); s.Verify(); Require(s.originals == 1 && s.finishes == 1, "replacement lifecycle");
        });
    }
    Case("prepare rejection releases partial cookie before native raise", [] {
        Scenario s; s.prepare_result = DCP_REJECT; s.expected_outcome = DCP_PREPARE_REJECTED;
        ExpectValidation(s, "prepare"); Require(s.originals == 0 && s.finishes == 0, "rejection entered original");
        Require(s.order == std::vector<std::string>{"prepare", "cleanup", "factory", "raise"}, "reject order");
    });
    Case("unknown prepare status fails closed with zero cookie", [] {
        Scenario s; s.cookie = 0; s.prepare_result = -91; s.expected_outcome = DCP_PREPARE_REJECTED;
        ExpectValidation(s, "prepare"); Require(s.originals == 0, "unknown status entered original");
    });
    Case("passthrough cannot smuggle an ignored replacement", [] {
        Scenario s; s.provide_replacement = true; s.expected_outcome = DCP_INVALID_PLAN;
        ExpectValidation(s, "replacement-stream"); Require(s.originals == 0, "invalid plan entered original");
    });
    Case("replacement requires a nonnull stream", [] {
        Scenario s; s.prepare_result = DCP_REPLACE_STREAM; s.expected_outcome = DCP_INVALID_PLAN;
        ExpectValidation(s, "replacement-stream"); Require(s.originals == 0, "null replacement entered original");
    });
    Case("finish failure raises after original and cleanup", [] {
        Scenario s; s.finish_result = 8; s.expected_outcome = DCP_FINISH_REJECTED;
        ExpectValidation(s, "finish"); Require(s.originals == 1 && s.finishes == 1, "finish failure lifecycle");
    });
    for (std::uint32_t site : {1U, 2U, 3U, 4U}) for (bool prepare : {true, false}) {
        const std::string label = "site " + std::to_string(site) + (prepare ? " prepare" : " finish") + " failure stays isolated";
        Case(label.c_str(), [=] {
            Scenario failing; failing.site = site; failing.operation = prepare ? DCP_DESERIALIZE : DCP_SERIALIZE;
            if (prepare) { failing.prepare_result = DCP_REJECT; failing.expected_outcome = DCP_PREPARE_REJECTED; }
            else { failing.finish_result = 6; failing.expected_outcome = DCP_FINISH_REJECTED; }
            ExpectValidation(failing, prepare ? "prepare" : "finish");
            Require(failing.originals == (prepare ? 0 : 1) && failing.finishes == (prepare ? 0 : 1), "cross-site failure lifecycle");
            for (std::uint32_t follow_site = 0; follow_site < 5; ++follow_site) {
                Scenario clean; clean.site = follow_site; clean.operation = DCP_DESERIALIZE;
                Invoke(clean); clean.Verify(); Require(clean.originals == 1 && clean.finishes == 1, "failed site poisoned a later call");
            }
        });
    }
    Case("cleanup failure cannot report successful serialization", [] {
        Scenario s; s.cleanup_result = 1; ExpectValidation(s, "cleanup");
    });
    Case("cleanup failure preserves primary preparation rejection", [] {
        Scenario s; s.prepare_result = DCP_REJECT; s.cleanup_result = 1; s.expected_outcome = DCP_PREPARE_REJECTED;
        ExpectValidation(s, "prepare");
    });
    Case("native original exception is preserved after one cleanup", [] {
        Scenario s; s.original_throws = true; s.expected_outcome = DCP_ORIGINAL_EXCEPTION;
        bool caught = false;
        try { Invoke(s); } catch (const OriginalError& error) { caught = error.identity == 41; }
        Require(caught, "native original exception changed"); s.Verify();
        Require(s.finishes == 0 && s.factories == 0 && s.raises == 0, "original exception translated");
    });
    Case("cleanup rejection does not replace native original exception", [] {
        Scenario s; s.original_throws = true; s.cleanup_result = 4; s.expected_outcome = DCP_ORIGINAL_EXCEPTION;
        bool caught = false;
        try { Invoke(s); } catch (const OriginalError& error) { caught = error.identity == 41; }
        Require(caught, "cleanup masked original"); s.Verify();
    });
    Case("cleanup native throw does not replace original exception", [] {
        Scenario s; s.original_throws = true; s.cleanup_throws = true; s.expected_outcome = DCP_ORIGINAL_EXCEPTION;
        bool caught = false;
        try { Invoke(s); } catch (const OriginalError& error) { caught = error.identity == 41; }
        Require(caught, "cleanup native throw masked original"); s.Verify();
    });
    for (bool prepare : {true, false}) {
        Case(prepare ? "mock native prepare throw unwinds with one cleanup" : "mock native finish throw unwinds with one cleanup", [=] {
            Scenario s; s.prepare_throws = prepare; s.finish_throws = !prepare; s.expected_outcome = DCP_CALLBACK_EXCEPTION;
            bool caught = false;
            try { Invoke(s); } catch (const CallbackError& error) { caught = error.identity == (prepare ? 17 : 19); }
            Require(caught, "native callback exception changed"); s.Verify();
            Require(s.factories == 0 && s.raises == 0, "callback contract violation translated");
        });
    }
    Case("mock native cleanup throw on success becomes cleanup rejection", [] {
        Scenario s; s.cleanup_throws = true; ExpectValidation(s, "cleanup");
    });
    Case("nested operations have independent cookies and lifetimes", [] {
        Scenario outer, inner; inner.operation = DCP_DESERIALIZE; inner.cookie = 2; inner.version = -123;
        inner.prepare_result = DCP_REPLACE_STREAM; inner.provide_replacement = true; outer.nested = &inner;
        Invoke(outer); outer.Verify(); inner.Verify();
        Require(outer.finishes == 1 && inner.finishes == 1, "nested successful finish");
    });
    Case("nested rejection unwinds the outer native original", [] {
        Scenario outer, inner; inner.operation = DCP_DESERIALIZE; inner.cookie = 3;
        inner.prepare_result = DCP_REJECT; inner.expected_outcome = DCP_PREPARE_REJECTED;
        outer.nested = &inner; outer.expected_outcome = DCP_ORIGINAL_EXCEPTION;
        bool caught = false;
        try { Invoke(outer); } catch (const ValidationError&) { caught = true; }
        Require(caught, "nested rejection swallowed"); outer.Verify(); inner.Verify();
        Require(outer.finishes == 0 && outer.factories == 0 && inner.factories == 1, "nested rejection duplicated");
    });
    Case("nested GameState command action sites keep distinct configurations", [] {
        Scenario outer, middle, inner;
        middle.site = 1; middle.operation = DCP_DESERIALIZE; middle.cookie = 12;
        inner.site = 2; inner.operation = DCP_SERIALIZE; inner.cookie = 24; inner.version = -123;
        inner.prepare_result = DCP_REPLACE_STREAM; inner.provide_replacement = true;
        outer.nested = &middle; middle.nested = &inner;
        Invoke(outer); outer.Verify(); middle.Verify(); inner.Verify();
        Require(outer.finishes == 1 && middle.finishes == 1 && inner.finishes == 1, "cross-site successful finish missing");
        Require(outer.factories == 0 && middle.factories == 0 && inner.factories == 0, "cross-site success rejected");
    });
    Case("action rejection unwinds command and GameState with one cleanup each", [] {
        Scenario outer, middle, inner;
        outer.operation = DCP_DESERIALIZE; outer.expected_outcome = DCP_ORIGINAL_EXCEPTION;
        middle.site = 1; middle.operation = DCP_DESERIALIZE; middle.cookie = 13; middle.expected_outcome = DCP_ORIGINAL_EXCEPTION;
        inner.site = 2; inner.operation = DCP_DESERIALIZE; inner.cookie = 25;
        inner.prepare_result = DCP_REJECT; inner.expected_outcome = DCP_PREPARE_REJECTED;
        outer.nested = &middle; middle.nested = &inner;
        bool caught = false;
        try { Invoke(outer); } catch (const ValidationError&) { caught = true; }
        Require(caught, "cross-site rejection swallowed"); outer.Verify(); middle.Verify(); inner.Verify();
        Require(outer.originals == 1 && middle.originals == 1 && inner.originals == 0, "cross-site original dispatch changed");
        Require(outer.finishes == 0 && middle.finishes == 0 && inner.finishes == 0, "cross-site failure falsely finished");
        Require(outer.factories == 0 && middle.factories == 0 && inner.factories == 1 && inner.raises == 1,
                "cross-site failure translated or raised twice");
    });
    for (std::uint32_t site : {3U, 4U}) for (int operation : {DCP_SERIALIZE, DCP_DESERIALIZE}) {
        const std::string label = "Train site " + std::to_string(site) + " direction " + std::to_string(operation);
        Case((label + " native original exception and cleanup fault remain primary").c_str(), [=] {
            Scenario s; s.site = site; s.operation = operation; s.original_throws = true;
            s.cleanup_throws = true; s.expected_outcome = DCP_ORIGINAL_EXCEPTION;
            bool caught = false;
            try { Invoke(s); } catch (const OriginalError& error) { caught = error.identity == 41; }
            Require(caught, "Train original exception changed or returned success"); s.Verify();
            Require(s.originals == 1 && s.finishes == 0 && s.factories == 0 && s.raises == 0,
                    "Train original exception translated or finished");
        });
        Case((label + " cleanup failure raises outside callbacks").c_str(), [=] {
            Scenario s; s.site = site; s.operation = operation; s.cleanup_result = 7;
            ExpectValidation(s, "cleanup");
            Require(s.originals == 1 && s.finishes == 1, "Train cleanup failure lifecycle");
        });
    }
    Case("nested GameState TrainCommand TrainAction keeps independent lifetimes", [] {
        Scenario outer, command, action;
        command.site = 3; command.operation = DCP_DESERIALIZE; command.cookie = 33;
        action.site = 4; action.operation = DCP_SERIALIZE; action.cookie = 44; action.version = -123;
        action.prepare_result = DCP_REPLACE_STREAM; action.provide_replacement = true;
        outer.nested = &command; command.nested = &action;
        Invoke(outer); outer.Verify(); command.Verify(); action.Verify();
        Require(outer.finishes == 1 && command.finishes == 1 && action.finishes == 1,
                "Train nested success missed finish");
        Require(outer.factories == 0 && command.factories == 0 && action.factories == 0,
                "Train nested success rejected");
    });
    Case("TrainAction rejection unwinds old Build and GameState parents once", [] {
        Scenario outer, build, train;
        outer.expected_outcome = DCP_ORIGINAL_EXCEPTION;
        build.site = 1; build.operation = DCP_DESERIALIZE; build.cookie = 31;
        build.expected_outcome = DCP_ORIGINAL_EXCEPTION;
        train.site = 4; train.operation = DCP_DESERIALIZE; train.cookie = 34;
        train.prepare_result = DCP_REJECT; train.expected_outcome = DCP_PREPARE_REJECTED;
        outer.nested = &build; build.nested = &train;
        bool caught = false;
        try { Invoke(outer); } catch (const ValidationError&) { caught = true; }
        Require(caught, "Train nested rejection returned success"); outer.Verify(); build.Verify(); train.Verify();
        Require(outer.originals == 1 && build.originals == 1 && train.originals == 0 &&
                outer.finishes == 0 && build.finishes == 0 && train.finishes == 0,
                "Train nested rejection changed original/finish dispatch");
        Require(outer.factories == 0 && build.factories == 0 && train.factories == 1 && train.raises == 1,
                "Train nested rejection raised more than once");
    });
    Case("native factory exception cannot duplicate cleanup", [] {
        Scenario s; s.prepare_result = DCP_REJECT; s.expected_outcome = DCP_PREPARE_REJECTED; s.factory_throws = true;
        bool caught = false; try { Invoke(s); } catch (const FactoryError&) { caught = true; }
        Require(caught, "factory exception lost"); s.Verify(); Require(s.raises == 0, "raised after throwing factory");
    });
    Case("null exception factory cannot return serializer success", [] {
        ExpectAbort([] { Scenario s; s.prepare_result = DCP_REJECT; s.expected_outcome = DCP_PREPARE_REJECTED;
                         s.factory_null = true; Invoke(s); });
    });
    Case("returning native raiser cannot return serializer success", [] {
        ExpectAbort([] { Scenario s; s.prepare_result = DCP_REJECT; s.expected_outcome = DCP_PREPARE_REJECTED;
                         s.raiser_returns = true; Invoke(s); });
    });
    Case("immutable configuration supports independent concurrent calls", [] {
        std::atomic<int> failed{0}; std::vector<std::thread> workers;
        for (int i = 0; i < 20; ++i) workers.emplace_back([&, i] {
            try {
                Scenario s; s.cookie = static_cast<std::uint64_t>(i); s.site = static_cast<std::uint32_t>(i % 5);
                s.operation = (i / 5) % 2;
                s.prepare_result = DCP_REPLACE_STREAM; s.provide_replacement = true;
                Invoke(s); s.Verify();
            } catch (...) { ++failed; }
        });
        for (auto& worker : workers) worker.join();
        Require(failed == 0, "concurrent operation state leaked");
    });
    std::printf("RESULT %d passed, %d failed. C++ control flow only; IL2CPP integration remains untested.\n", Passed, Failed);
#if defined(_WIN32)
    if (RequestedAbortProbe != 0) return 3; // A requested probe must have aborted.
#endif
    return Failed == 0 ? 0 : 1;
}
