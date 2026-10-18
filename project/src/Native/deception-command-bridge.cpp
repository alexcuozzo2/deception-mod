// DRAFT: typed CommandBase ToByteArray / FromByteArray native boundary.
// No detours, native lookup, format detection or managed implementation.
// Callbacks must contain CLR exceptions. A caller-owned managed invocation
// must root every handled result through native return and acknowledgement;
// cleanup must not release that result lease. This ABI itself grants no lease.
#include <cstddef>
#include <cstdint>
#include <type_traits>
#include "deception-platform.h"
#define DCP_CMD_EXPORT DCP_EXPORT
enum : std::int32_t { CMD_PASS=0, CMD_HANDLED=1, CMD_REJECT=2 };
enum : std::int32_t { CMD_COMPLETE=0, CMD_PREPARE_REJECTED=1, CMD_INVALID_PLAN=2,
    CMD_FINISH_REJECTED=3, CMD_ORIGINAL_EXCEPTION=4, CMD_CALLBACK_EXCEPTION=5 };
enum : std::int32_t { CMD_CONFIGURED=0, CMD_CONFIG_NULL=1, CMD_CONFIG_LAYOUT=2,
    CMD_CONFIG_POINTER=3, CMD_CONFIG_NAME=4, CMD_CONFIG_ALREADY_SET=5, CMD_CONFIG_INTERNAL=6 };
struct DcpCmdCall {
    std::uint32_t struct_size, operation; // 0 ToByteArray, 1 FromByteArray.
    std::int32_t version; std::uint32_t reserved; // input version only for write.
    void* input; // CommandBase* for write, native Byte[]* for read.
    void** command_out; std::int32_t* version_out; // exact original output slots.
    const void* method_info;
};
struct DcpCmdValue {
    void* value; // Byte[] for write, CommandBase for read.
    std::int32_t version;
    std::uint32_t boolean; // exact UInt8 native result promoted to UInt32.
};
struct DcpCmdPlan { std::uint64_t cookie; DcpCmdValue handled; };
using DcpCmdWrite=void* (*)(void*,std::int32_t,const void*);
using DcpCmdRead=std::uint8_t (*)(void*,void**,std::int32_t*,const void*);
using DcpCmdPrepare=std::int32_t (*)(const DcpCmdCall*,DcpCmdPlan*);
using DcpCmdFinish=std::int32_t (*)(const DcpCmdCall*,const DcpCmdPlan*,const DcpCmdValue*,std::int32_t);
using DcpCmdCleanup=std::int32_t (*)(const DcpCmdCall*,const DcpCmdPlan*,std::int32_t);
using DcpCmdFactory=void* (*)(const void*,const char*,const char*,const char*);
using DcpCmdRaise=void (*)(void*);
struct DcpCmdConfig {
    std::uint32_t struct_size, abi_version;
    DcpCmdWrite write; DcpCmdRead read;
    DcpCmdPrepare prepare; DcpCmdFinish finish; DcpCmdCleanup cleanup;
    DcpCmdFactory factory; DcpCmdRaise raise;
    const void* exception_image; const char* exception_namespace; const char* exception_name;
};
static_assert(sizeof(void*)==8 && sizeof(std::uint8_t)==1);
static_assert(std::is_standard_layout<DcpCmdCall>::value && std::is_trivial<DcpCmdCall>::value);
static_assert(sizeof(DcpCmdCall)==48 && alignof(DcpCmdCall)==8);
static_assert(offsetof(DcpCmdCall,operation)==4 && offsetof(DcpCmdCall,version)==8 && offsetof(DcpCmdCall,reserved)==12);
static_assert(offsetof(DcpCmdCall,input)==16 && offsetof(DcpCmdCall,command_out)==24 && offsetof(DcpCmdCall,version_out)==32 && offsetof(DcpCmdCall,method_info)==40);
static_assert(sizeof(DcpCmdValue)==16 && offsetof(DcpCmdValue,version)==8 && offsetof(DcpCmdValue,boolean)==12);
static_assert(sizeof(DcpCmdPlan)==24 && offsetof(DcpCmdPlan,handled)==8);
static_assert(sizeof(DcpCmdConfig)==88 && offsetof(DcpCmdConfig,write)==8 && offsetof(DcpCmdConfig,read)==16);
static_assert(offsetof(DcpCmdConfig,prepare)==24 && offsetof(DcpCmdConfig,finish)==32 && offsetof(DcpCmdConfig,cleanup)==40);
static_assert(offsetof(DcpCmdConfig,factory)==48 && offsetof(DcpCmdConfig,raise)==56 && offsetof(DcpCmdConfig,exception_image)==64);
static_assert(offsetof(DcpCmdConfig,exception_namespace)==72 && offsetof(DcpCmdConfig,exception_name)==80);
DCP_CMD_EXPORT std::uint32_t dcp_cmd_bridge_abi_version() noexcept;
DCP_CMD_EXPORT std::uint32_t dcp_cmd_bridge_call_size() noexcept;
DCP_CMD_EXPORT std::uint32_t dcp_cmd_bridge_value_size() noexcept;
DCP_CMD_EXPORT std::uint32_t dcp_cmd_bridge_plan_size() noexcept;
DCP_CMD_EXPORT std::uint32_t dcp_cmd_bridge_config_size() noexcept;
DCP_CMD_EXPORT std::int32_t dcp_cmd_bridge_configure(const DcpCmdConfig*) noexcept;
DCP_CMD_EXPORT void* dcp_cmd_bridge_to_bytes(void*,std::int32_t,const void*);
DCP_CMD_EXPORT std::uint8_t dcp_cmd_bridge_from_bytes(void*,void**,std::int32_t*,const void*);
#ifndef DCP_CMD_DECLARATIONS_ONLY
#include <atomic>
#include <cstdlib>
#include <cstring>
#include <mutex>
namespace {
std::atomic<bool> Configured{false}; std::mutex ConfigLock; DcpCmdConfig Config{};
char NameSpace[128]{}, Name[128]{};
bool BoundedName(const char* value,bool empty,std::size_t& n) noexcept {
    if(!value)return false;
    for(n=0;n<128;++n)if(value[n]=='\0')return empty||n!=0;
    return false;
}
[[noreturn]] void Reject(const char* why) {
    void* error=Config.factory(Config.exception_image,Config.exception_namespace,Config.exception_name,why);
    if (!error) std::abort();
    Config.raise(error);
    std::abort();
}
void Clear(const DcpCmdCall& call) noexcept {
    if(call.operation==1){if(call.command_out)*call.command_out=nullptr;if(call.version_out)*call.version_out=0;}
}
class CleanupOnce final {
    const DcpCmdCall& call; const DcpCmdPlan& plan; bool done=false;
public:
    CleanupOnce(const DcpCmdCall& c,const DcpCmdPlan& p):call(c),plan(p){}
    CleanupOnce(const CleanupOnce&)=delete;
    std::int32_t Run(std::int32_t outcome) noexcept {
        if (done) return 0;
        done = true;
        try{return Config.cleanup(&call,&plan,outcome);}catch(...){return -1;}
    }
    ~CleanupOnce() noexcept {Run(CMD_CALLBACK_EXCEPTION);}
};
bool Valid(const DcpCmdCall& call,const DcpCmdPlan& plan,std::int32_t prepared) noexcept {
    const auto& v=plan.handled;
    if(prepared==CMD_PASS)return !v.value&&v.version==0&&v.boolean==0;
    if(prepared!=CMD_HANDLED)return false;
    if(call.operation==0)return v.value&&v.version==0&&v.boolean==0;
    if(!call.command_out||!call.version_out||v.boolean>1)return false;
    return v.boolean==1?v.value!=nullptr:(!v.value&&v.version==0);
}
DcpCmdValue Invoke(const DcpCmdCall& call) {
    if(!Configured.load(std::memory_order_acquire))std::abort();
    DcpCmdPlan plan{}; CleanupOnce cleanup(call,plan); std::int32_t prepared;
    try{prepared=Config.prepare(&call,&plan);}catch(...){
        cleanup.Run(CMD_CALLBACK_EXCEPTION);Clear(call);Reject("Command prepare callback violated its no-throw contract.");
    }
    if(!Valid(call,plan,prepared)){
        cleanup.Run(prepared==CMD_REJECT?CMD_PREPARE_REJECTED:CMD_INVALID_PLAN);
        Clear(call);Reject("Command prepare rejected the operation or returned an invalid plan.");
    }
    const std::int32_t ran=prepared==CMD_PASS?1:0;
    DcpCmdValue result=plan.handled;
    if(ran){
        try{
            if(call.operation==0)result={Config.write(call.input,call.version,call.method_info),0,0};
            else{
                std::uint8_t value=Config.read(call.input,call.command_out,call.version_out,call.method_info);
                result={call.command_out?*call.command_out:nullptr,call.version_out?*call.version_out:0,value};
            }
        }catch(...){
            cleanup.Run(CMD_ORIGINAL_EXCEPTION);
            throw; // Preserve genuine native exception AND its raw output-slot behavior.
        }
    }
    std::int32_t finished;
    try{finished=Config.finish(&call,&plan,&result,ran);}catch(...){
        cleanup.Run(CMD_CALLBACK_EXCEPTION);if(!ran)Clear(call);
        Reject("Command finish callback violated its no-throw contract.");
    }
    if(finished!=0){cleanup.Run(CMD_FINISH_REJECTED);if(!ran)Clear(call);Reject("Command finish rejected the operation.");}
    if(cleanup.Run(CMD_COMPLETE)!=0){if(!ran)Clear(call);Reject("Command cleanup failed.");}
    // The caller-owned result lease remains live after cleanup. Publish handled
    // outputs only now; no result may escape on a failed outer transaction.
    if(!ran&&call.operation==1){*call.command_out=result.value;*call.version_out=result.version;}
    return result;
}
}
DCP_CMD_EXPORT std::uint32_t dcp_cmd_bridge_abi_version() noexcept{return 1;}
DCP_CMD_EXPORT std::uint32_t dcp_cmd_bridge_call_size() noexcept{return sizeof(DcpCmdCall);}
DCP_CMD_EXPORT std::uint32_t dcp_cmd_bridge_value_size() noexcept{return sizeof(DcpCmdValue);}
DCP_CMD_EXPORT std::uint32_t dcp_cmd_bridge_plan_size() noexcept{return sizeof(DcpCmdPlan);}
DCP_CMD_EXPORT std::uint32_t dcp_cmd_bridge_config_size() noexcept{return sizeof(DcpCmdConfig);}
DCP_CMD_EXPORT std::int32_t dcp_cmd_bridge_configure(const DcpCmdConfig* supplied) noexcept {
    try{
        std::lock_guard<std::mutex> lock(ConfigLock);
        if(Configured.load(std::memory_order_relaxed))return CMD_CONFIG_ALREADY_SET;
        if(!supplied)return CMD_CONFIG_NULL;
        if(supplied->struct_size!=sizeof(DcpCmdConfig)||supplied->abi_version!=1)return CMD_CONFIG_LAYOUT;
        auto c=*supplied;
        if(!c.write||!c.read||c.write==dcp_cmd_bridge_to_bytes||c.read==dcp_cmd_bridge_from_bytes||
           reinterpret_cast<void*>(c.write)==reinterpret_cast<void*>(dcp_cmd_bridge_from_bytes)||
           reinterpret_cast<void*>(c.read)==reinterpret_cast<void*>(dcp_cmd_bridge_to_bytes)||
           !c.prepare||!c.finish||!c.cleanup||!c.factory||!c.raise||!c.exception_image)return CMD_CONFIG_POINTER;
        std::size_t ns=0,n=0;
        if(!BoundedName(c.exception_namespace,true,ns)||!BoundedName(c.exception_name,false,n))return CMD_CONFIG_NAME;
        std::memcpy(NameSpace,c.exception_namespace,ns+1);std::memcpy(Name,c.exception_name,n+1);
        Config=c;Config.exception_namespace=NameSpace;Config.exception_name=Name;
        Configured.store(true,std::memory_order_release);return CMD_CONFIGURED;
    }catch(...){return CMD_CONFIG_INTERNAL;}
}
DCP_CMD_EXPORT void* dcp_cmd_bridge_to_bytes(void* input,std::int32_t version,const void* method){
    return Invoke({sizeof(DcpCmdCall),0,version,0,input,nullptr,nullptr,method}).value;
}
DCP_CMD_EXPORT std::uint8_t dcp_cmd_bridge_from_bytes(void* input,void** command,std::int32_t* version,const void* method){
    return static_cast<std::uint8_t>(Invoke({sizeof(DcpCmdCall),1,0,0,input,command,version,method}).boolean);
}
#endif
