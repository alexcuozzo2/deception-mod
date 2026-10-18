// Standalone C++ control-flow tests only. No IL2CPP/game/framework dependency.
#define DCP_EXEC_DECLARATIONS_ONLY
#include "../../src/Native/deception-execution-bridge.cpp"
#include <array>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
int Passed = 0, CallbackDepth = 0, Factories = 0, Raises = 0, ContractViolations = 0;
int Objects[12]{};
std::string LastSpace, LastName, LastMessage;
std::vector<std::string> Trace;
void Check(bool ok, const std::string& name) {
    if (!ok) throw std::runtime_error("FAIL: " + name);
    ++Passed;
}
void Require(bool ok, const char* message) {
    if (!ok) { ++ContractViolations; throw std::logic_error(message); }
}
struct NativeError : std::runtime_error { using std::runtime_error::runtime_error; };
struct CallbackScope { CallbackScope() { ++CallbackDepth; } ~CallbackScope() { --CallbackDepth; } };
struct Runtime {
    int prepare_result = DCP_EXEC_PASS, finish_result = 0, cleanup_result = 0;
    bool prepare_throw = false, finish_throw = false, cleanup_throw = false, original_throw = false, zero_cookie = false;
    int prepared = 0, originals = 0, finished = 0, cleaned = 0, ran = -1, outcome = -1;
    int nested_site = -1;
};
std::array<Runtime,4> Runs;
const std::array<DcpExecOriginal,4> Entries = {
    dcp_exec_bridge_build_command, dcp_exec_bridge_build_action,
    dcp_exec_bridge_train_command, dcp_exec_bridge_train_action
};
void Reset() {
    Runs = {}; Trace.clear(); Factories = Raises = 0;
    LastSpace.clear(); LastName.clear(); LastMessage.clear();
    Require(CallbackDepth == 0, "Callback stack leaked");
}
void Call(unsigned site) { Entries.at(site)(&Objects[site], &Objects[4+site], &Objects[8+site]); }
void Identity(const DcpExecCall* call) {
    Require(call && call->struct_size == 32 && call->site < 4, "Wrong call ABI");
    Require(call->instance == &Objects[call->site] && call->state == &Objects[4+call->site] &&
        call->method_info == &Objects[8+call->site], "Native arguments changed");
}
std::uint64_t Cookie(unsigned site) { return Runs[site].zero_cookie ? 0 : 0xc0010000ULL + site; }
int Prepare(const DcpExecCall* call, DcpExecPlan* plan) {
    CallbackScope callback; Identity(call); auto& r = Runs[call->site]; ++r.prepared;
    Trace.push_back("prepare"+std::to_string(call->site));
    Require(plan && plan->cookie == 0, "Plan was not zero-initialized");
    plan->cookie = Cookie(call->site);
    if (r.prepare_throw) throw NativeError("mock prepare throw");
    return r.prepare_result;
}
int PrepareTrain(const DcpExecCall* call, DcpExecPlan* plan) {
    Require(call->site >= 2, "Training configuration crossed into build site");
    return Prepare(call,plan);
}
int Finish(const DcpExecCall* call, const DcpExecPlan* plan, int ran) {
    CallbackScope callback; Identity(call); auto& r = Runs[call->site]; ++r.finished; r.ran = ran;
    Trace.push_back("finish"+std::to_string(call->site));
    Require(plan && plan->cookie == Cookie(call->site), "Finish cookie changed");
    Require(ran == 0 || ran == 1, "Original-ran was not exact Int32 0/1");
    if (r.finish_throw) throw NativeError("mock finish throw");
    return r.finish_result;
}
int Cleanup(const DcpExecCall* call, const DcpExecPlan* plan, int outcome) {
    CallbackScope callback; Identity(call); auto& r = Runs[call->site]; ++r.cleaned; r.outcome = outcome;
    Trace.push_back("cleanup"+std::to_string(call->site));
    Require(plan && plan->cookie == Cookie(call->site), "Cleanup cookie changed");
    if (r.cleanup_throw) throw NativeError("mock cleanup throw");
    return r.cleanup_result;
}
void Original(unsigned site, void* self, void* state, const void* method) {
    Require(CallbackDepth == 0, "Original called with a callback still active");
    DcpExecCall call{32,site,self,state,method}; Identity(&call);
    auto& r = Runs[site]; ++r.originals; Trace.push_back("original"+std::to_string(site));
    const int nested = r.nested_site; r.nested_site = -1;
    if (nested >= 0) Call(static_cast<unsigned>(nested));
    if (r.original_throw) throw NativeError("original-native-"+std::to_string(site));
}
void Original0(void* a,void* b,const void* c) { Original(0,a,b,c); }
void Original1(void* a,void* b,const void* c) { Original(1,a,b,c); }
void Original2(void* a,void* b,const void* c) { Original(2,a,b,c); }
void Original3(void* a,void* b,const void* c) { Original(3,a,b,c); }
void* Factory(const void* image,const char* space,const char* name,const char* message) {
    Require(CallbackDepth == 0, "Factory called before callback returned");
    Require(image == &Objects[0], "Exception image changed");
    ++Factories; LastSpace=space; LastName=name; LastMessage=message; return &Objects[1];
}
void Raise(void* exception) {
    Require(CallbackDepth == 0 && exception == &Objects[1], "Raise called in callback or with wrong exception");
    ++Raises; throw NativeError("bridge-rejection");
}
DcpExecConfig Config(unsigned site) {
    const std::array<DcpExecOriginal,4> originals={Original0,Original1,Original2,Original3};
    return {80,1,originals[site],site<2?Prepare:PrepareTrain,Finish,Cleanup,Factory,Raise,&Objects[0],"System","InvalidOperationException"};
}
bool Throws(unsigned site,const std::string& expected) {
    try { Call(site); return false; }
    catch (const NativeError& error) { return error.what() == expected; }
}
void Counts(unsigned site,int originals,int finishes,int cleanups,int outcome) {
    auto& r=Runs[site];
    Check(r.prepared==1&&r.originals==originals&&r.finished==finishes&&r.cleaned==cleanups&&r.outcome==outcome,
        "exact callback/original counts site"+std::to_string(site));
}
void Configuration() {
    Check(dcp_exec_bridge_abi_version()==1&&dcp_exec_bridge_call_size()==32&&dcp_exec_bridge_plan_size()==8&&
        dcp_exec_bridge_config_size()==80&&dcp_exec_bridge_site_count()==4,"exported execution ABI");
    auto good=Config(0), bad=good;
    Check(dcp_exec_bridge_configure_site(4,&good)==DCP_EXEC_CONFIG_SITE,"reject out-of-range site");
    Check(dcp_exec_bridge_configure_site(0,nullptr)==DCP_EXEC_CONFIG_NULL,"reject null config");
    bad.struct_size=88;Check(dcp_exec_bridge_configure_site(0,&bad)==DCP_EXEC_CONFIG_LAYOUT,"reject serializer-sized config");
    bad=good;bad.abi_version=2;Check(dcp_exec_bridge_configure_site(0,&bad)==DCP_EXEC_CONFIG_LAYOUT,"reject wrong ABI version");
    for (unsigned i=0;i<4;++i) {
        bad=good;bad.original=Entries[i];
        Check(dcp_exec_bridge_configure_site(0,&bad)==DCP_EXEC_CONFIG_POINTER,"reject bridge original cycle"+std::to_string(i));
    }
    bad=good;bad.original=nullptr;Check(dcp_exec_bridge_configure_site(0,&bad)==DCP_EXEC_CONFIG_POINTER,"reject null original");
    bad=good;bad.prepare=nullptr;Check(dcp_exec_bridge_configure_site(0,&bad)==DCP_EXEC_CONFIG_POINTER,"reject null prepare");
    bad=good;bad.finish=nullptr;Check(dcp_exec_bridge_configure_site(0,&bad)==DCP_EXEC_CONFIG_POINTER,"reject null finish");
    bad=good;bad.cleanup=nullptr;Check(dcp_exec_bridge_configure_site(0,&bad)==DCP_EXEC_CONFIG_POINTER,"reject null cleanup");
    bad=good;bad.factory=nullptr;Check(dcp_exec_bridge_configure_site(0,&bad)==DCP_EXEC_CONFIG_POINTER,"reject null factory");
    bad=good;bad.raise=nullptr;Check(dcp_exec_bridge_configure_site(0,&bad)==DCP_EXEC_CONFIG_POINTER,"reject null raise");
    bad=good;bad.exception_image=nullptr;Check(dcp_exec_bridge_configure_site(0,&bad)==DCP_EXEC_CONFIG_POINTER,"reject null image");
    bad=good;bad.exception_name="";Check(dcp_exec_bridge_configure_site(0,&bad)==DCP_EXEC_CONFIG_EXCEPTION_NAME,"reject empty exception name");
    std::string long_name(128,'x');bad=good;bad.exception_name=long_name.c_str();
    Check(dcp_exec_bridge_configure_site(0,&bad)==DCP_EXEC_CONFIG_EXCEPTION_NAME,"bound exception name");
    char space[]="System", name[]="InvalidOperationException";
    good.exception_namespace=space;good.exception_name=name;
    Check(dcp_exec_bridge_configure_site(0,&good)==DCP_EXEC_CONFIGURED,"configure first site");
    space[0]='X';name[0]='X'; // Bridge must own both strings.
    Check(dcp_exec_bridge_configure_site(0,&good)==DCP_EXEC_CONFIG_ALREADY_SET,"first site immutable");
    for(unsigned i=1;i<4;++i) {
        auto config=Config(i);if(i==3)config.exception_namespace="";
        Check(dcp_exec_bridge_configure_site(i,&config)==DCP_EXEC_CONFIGURED,"configure independent site"+std::to_string(i));
        Check(dcp_exec_bridge_configure_site(i,&config)==DCP_EXEC_CONFIG_ALREADY_SET,"independent site immutable"+std::to_string(i));
    }
}
void Cases() {
    for(unsigned i=0;i<4;++i) {
        Reset();Call(i);Counts(i,1,1,1,DCP_EXEC_COMPLETE);
        Check(Runs[i].ran==1&&Factories==0&&Raises==0,"ordinary native pass-through site"+std::to_string(i));
        Reset();Runs[i].prepare_result=DCP_EXEC_HANDLED;Call(i);Counts(i,0,1,1,DCP_EXEC_COMPLETE);
        Check(Runs[i].ran==0&&Factories==0&&Raises==0,"handled skips original but finishes site"+std::to_string(i));
    }
    Reset();Runs[0].zero_cookie=true;Call(0);Counts(0,1,1,1,DCP_EXEC_COMPLETE);
    Reset();Runs[0].prepare_result=DCP_EXEC_REJECT;
    Check(Throws(0,"bridge-rejection"),"prepare reject raises native exception");Counts(0,0,0,1,DCP_EXEC_PREPARE_REJECTED);
    Check(Factories==1&&Raises==1&&LastSpace=="System"&&LastName=="InvalidOperationException","native raise after callbacks and copied names");
    Reset();Runs[0].prepare_result=99;
    Check(Throws(0,"bridge-rejection"),"unknown prepare status rejects");Counts(0,0,0,1,DCP_EXEC_INVALID_PREPARE);
    Reset();Runs[1].finish_result=1;
    Check(Throws(1,"bridge-rejection"),"finish rejection after original");Counts(1,1,1,1,DCP_EXEC_FINISH_REJECTED);
    Reset();Runs[1].prepare_result=DCP_EXEC_HANDLED;Runs[1].finish_result=1;
    Check(Throws(1,"bridge-rejection"),"handled finish rejection");Counts(1,0,1,1,DCP_EXEC_FINISH_REJECTED);
    Reset();Runs[2].original_throw=true;
    Check(Throws(2,"original-native-2"),"original native exception identity rethrown");Counts(2,1,0,1,DCP_EXEC_ORIGINAL_EXCEPTION);
    Check(Factories==0&&Raises==0,"original exception is not replaced");
    Reset();Runs[2].original_throw=true;Runs[2].cleanup_result=1;
    Check(Throws(2,"original-native-2"),"cleanup failure preserves original exception");Counts(2,1,0,1,DCP_EXEC_ORIGINAL_EXCEPTION);
    Reset();Runs[2].original_throw=true;Runs[2].cleanup_throw=true;
    Check(Throws(2,"original-native-2"),"cleanup mock native throw preserves original");Counts(2,1,0,1,DCP_EXEC_ORIGINAL_EXCEPTION);
    Reset();Runs[3].cleanup_result=1;
    Check(Throws(3,"bridge-rejection"),"normal cleanup failure rejects");Counts(3,1,1,1,DCP_EXEC_COMPLETE);
    Check(LastSpace.empty(),"empty configured exception namespace preserved");
    Reset();Runs[0].prepare_throw=true;
    Check(Throws(0,"bridge-rejection"),"mock callback throw fails closed");Counts(0,0,0,1,DCP_EXEC_CALLBACK_EXCEPTION);
    Reset();Runs[0].finish_throw=true;
    Check(Throws(0,"bridge-rejection"),"mock finish throw fails closed");Counts(0,1,1,1,DCP_EXEC_CALLBACK_EXCEPTION);
    Reset();Runs[0].cleanup_throw=true;
    Check(Throws(0,"bridge-rejection"),"mock cleanup throw fails closed");Counts(0,1,1,1,DCP_EXEC_COMPLETE);
    Reset();Runs[0].nested_site=1;Call(0);
    Counts(0,1,1,1,DCP_EXEC_COMPLETE);Counts(1,1,1,1,DCP_EXEC_COMPLETE);
    Check(Trace==std::vector<std::string>{"prepare0","original0","prepare1","original1","finish1","cleanup1","finish0","cleanup0"},"cross-site nested exact order");
    Reset();Runs[0].nested_site=3;Runs[3].prepare_result=DCP_EXEC_HANDLED;Call(0);
    Counts(0,1,1,1,DCP_EXEC_COMPLETE);Counts(3,0,1,1,DCP_EXEC_COMPLETE);
    Reset();Runs[0].nested_site=1;Runs[1].original_throw=true;
    Check(Throws(0,"original-native-1"),"nested original failure propagates unchanged");
    Counts(0,1,0,1,DCP_EXEC_ORIGINAL_EXCEPTION);Counts(1,1,0,1,DCP_EXEC_ORIGINAL_EXCEPTION);
    Reset();Runs[0].nested_site=1;Runs[1].prepare_result=DCP_EXEC_REJECT;
    Check(Throws(0,"bridge-rejection"),"nested reject propagates through outer original");
    Counts(0,1,0,1,DCP_EXEC_ORIGINAL_EXCEPTION);Counts(1,0,0,1,DCP_EXEC_PREPARE_REJECTED);
    Check(Factories==1&&Raises==1,"nested rejection creates one native exception");
    Reset();Runs[0].prepare_result=DCP_EXEC_REJECT;Check(Throws(0,"bridge-rejection"),"site0 isolated failure");
    Call(2);Counts(2,1,1,1,DCP_EXEC_COMPLETE);
    Check(Runs[1].prepared==0&&Runs[3].prepared==0,"uninvoked site state unchanged");
    Check(CallbackDepth==0,"all callback frames released");
}
} // namespace

int main() {
    try {
        Configuration();Cases();
        Check(ContractViolations==0,"no unexpected callback assertion was hidden by an expected rejection");
        std::cout<<"PASS "<<Passed<<" native execution bridge control-flow assertions; mock native functions only.\n";
        return 0;
    } catch(const std::exception& error) {
        std::cerr<<error.what()<<"\n";return 1;
    }
}
