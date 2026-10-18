// Synthetic native ABI/control-flow tests. No game or managed callbacks.
#define DCP_CMD_DECLARATIONS_ONLY
#include "../../src/Native/deception-command-bridge.cpp"
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>
#include <limits>
namespace {
int Passed=0,Depth=0,Objects[8]{};
struct Error:std::runtime_error{using std::runtime_error::runtime_error;};
void Check(bool ok,const char* why){if(!ok)throw Error(why);++Passed;}
void Need(bool ok){if(!ok)throw std::logic_error("Callback contract mismatch");}
struct Callback {Callback(){++Depth;}~Callback(){--Depth;}};
struct Run {
    int mode=CMD_PASS,finish=0,cleanup=0,prepared=0,finished=0,cleaned=0,originals=0,raised=0,outcome=-1;
    bool prepareThrow=false,finishThrow=false,cleanupThrow=false,originalThrow=false,nested=false,invalid=false,zeroCookie=false;
    std::uint32_t op=0;std::int32_t version=-1234567;
    void* result=&Objects[2];std::int32_t readVersion=-77;std::uint8_t readBool=1;
    void* command=&Objects[3];std::int32_t outVersion=678;
    void** expectedCommand=nullptr;std::int32_t* expectedVersion=nullptr;
    DcpCmdValue handled{&Objects[4],123,1};
    bool exactName=true;
} R;
void Identity(const DcpCmdCall* c){
    Need(c&&c->struct_size==48&&c->operation==R.op&&c->reserved==0&&c->input==&Objects[0]&&c->method_info==&Objects[1]);
    Need(c->version==(R.op==0?R.version:0)&&c->command_out==R.expectedCommand&&c->version_out==R.expectedVersion);
}
std::uint64_t Cookie(){return R.zeroCookie?0:0x123456789ABCDEF0ULL;}
int Prepare(const DcpCmdCall* c,DcpCmdPlan* p){
    Callback cb;Identity(c);++R.prepared;Need(p&&p->cookie==0&&!p->handled.value&&p->handled.version==0&&p->handled.boolean==0);
    p->cookie=Cookie();if(R.mode==CMD_HANDLED||R.invalid)p->handled=R.handled;
    if(R.prepareThrow)throw Error("prepare");
    return R.mode;
}
int Finish(const DcpCmdCall* c,const DcpCmdPlan* p,const DcpCmdValue* result,int ran){
    Callback cb;Identity(c);++R.finished;Need(p->cookie==Cookie()&&ran==(R.mode==CMD_PASS?1:0));
    if(ran)Need(result->value==(R.op==0?R.result:(c->command_out?R.result:nullptr))&&
        result->version==(R.op==0||!c->version_out?0:R.readVersion)&&result->boolean==(R.op==0?0u:static_cast<std::uint32_t>(R.readBool)));
    else{
        Need(result->value==R.handled.value&&result->version==R.handled.version&&result->boolean==R.handled.boolean);
        if(c->command_out)Need(*c->command_out==&Objects[3]);
        if(c->version_out)Need(*c->version_out==678);
    }
    if(R.finishThrow)throw Error("finish");
    return R.finish;
}
int Cleanup(const DcpCmdCall* c,const DcpCmdPlan* p,int outcome){
    Callback cb;Identity(c);++R.cleaned;Need(p->cookie==Cookie());R.outcome=outcome;
    // Handled publication must wait for this cleanup, even on success.
    if(R.mode==CMD_HANDLED){if(c->command_out)Need(*c->command_out==&Objects[3]);if(c->version_out)Need(*c->version_out==678);}
    if(R.cleanupThrow)throw Error("cleanup");
    return R.cleanup;
}
void Original(){Need(Depth==0);++R.originals;}
DcpCmdValue Call();
void Nested(){
    if(!R.nested)return;
    Run outer=R;
    R=Run{};R.op=1;R.mode=CMD_HANDLED;R.expectedCommand=&R.command;R.expectedVersion=&R.outVersion;
    auto child=Call();Need(child.value==&Objects[4]&&child.version==123&&child.boolean==1&&R.cleaned==1&&R.originals==0);
    R=outer;R.nested=false;
}
void* Write(void* input,std::int32_t version,const void* method){
    Need(input==&Objects[0]&&method==&Objects[1]&&version==R.version);Original();
    Nested();
    if(R.originalThrow)throw Error("original");
    return R.result;
}
std::uint8_t Read(void* input,void** command,std::int32_t* version,const void* method){
    Need(input==&Objects[0]&&method==&Objects[1]&&command==R.expectedCommand&&version==R.expectedVersion);Original();
    Nested();
    if(command)*command=R.result;
    if(version)*version=R.readVersion;
    if(R.originalThrow)throw Error("original");
    return R.readBool;
}
void* Factory(const void* image,const char* ns,const char* name,const char*){
    Need(Depth==0&&image==&Objects[5]);R.exactName=std::string(ns)=="System"&&std::string(name)=="InvalidOperationException";return &Objects[6];
}
void Raise(void* value){Need(Depth==0&&value==&Objects[6]);++R.raised;throw Error("reject");}
void Reset(unsigned op){Need(Depth==0);R=Run{};R.op=op;if(op==0)R.handled={&Objects[4],0,0};else{R.expectedCommand=&R.command;R.expectedVersion=&R.outVersion;}}
DcpCmdValue Call(){
    if(R.op==0)return {dcp_cmd_bridge_to_bytes(&Objects[0],R.version,&Objects[1]),0,0};
    auto b=dcp_cmd_bridge_from_bytes(&Objects[0],R.expectedCommand,R.expectedVersion,&Objects[1]);
    return {R.command,R.outVersion,b};
}
void Fails(const char* message){bool failed=false;try{Call();}catch(const Error& e){failed=e.what()==std::string(message);}Check(failed,"Expected exact native exception");}
void Counts(int original,int finish,int cleanup,int outcome){Check(R.prepared==1&&R.originals==original&&R.finished==finish&&R.cleaned==cleanup&&R.outcome==outcome,"Exact callback counts/outcome");}
void Configuration(){
    Check(dcp_cmd_bridge_abi_version()==1&&dcp_cmd_bridge_call_size()==48&&dcp_cmd_bridge_value_size()==16&&dcp_cmd_bridge_plan_size()==24&&dcp_cmd_bridge_config_size()==88,"ABI layout exports");
    DcpCmdConfig c{88,1,Write,Read,Prepare,Finish,Cleanup,Factory,Raise,&Objects[5],"System","InvalidOperationException"};
    Check(dcp_cmd_bridge_configure(nullptr)==CMD_CONFIG_NULL,"Null config");
    auto bad=c;bad.struct_size=80;Check(dcp_cmd_bridge_configure(&bad)==CMD_CONFIG_LAYOUT,"Wrong config layout");
    bad=c;bad.abi_version=2;Check(dcp_cmd_bridge_configure(&bad)==CMD_CONFIG_LAYOUT,"Wrong ABI");
    bad=c;bad.write=dcp_cmd_bridge_to_bytes;Check(dcp_cmd_bridge_configure(&bad)==CMD_CONFIG_POINTER,"Write recursion");
    bad=c;bad.read=dcp_cmd_bridge_from_bytes;Check(dcp_cmd_bridge_configure(&bad)==CMD_CONFIG_POINTER,"Read recursion");
    for(int i=0;i<9;++i){
        bad=c;
        switch(i){case 0:bad.write=nullptr;break;case 1:bad.read=nullptr;break;case 2:bad.prepare=nullptr;break;case 3:bad.finish=nullptr;break;case 4:bad.cleanup=nullptr;break;case 5:bad.factory=nullptr;break;case 6:bad.raise=nullptr;break;case 7:bad.exception_image=nullptr;break;
        case 8:
            // Supply the wrong-signature entry as an address, just as the
            // production recursion guard compares it. Never call this pointer.
            bad.write=reinterpret_cast<DcpCmdWrite>(reinterpret_cast<void*>(dcp_cmd_bridge_from_bytes));
            break;
        }
        Check(dcp_cmd_bridge_configure(&bad)==CMD_CONFIG_POINTER,"Required pointer/typed recursion");
    }
    bad=c;bad.exception_name="";Check(dcp_cmd_bridge_configure(&bad)==CMD_CONFIG_NAME,"Empty exception name");
    std::string longName(128,'x');bad=c;bad.exception_namespace=longName.c_str();Check(dcp_cmd_bridge_configure(&bad)==CMD_CONFIG_NAME,"Bounded exception namespace");
    char ns[]="System",name[]="InvalidOperationException";c.exception_namespace=ns;c.exception_name=name;
    Check(dcp_cmd_bridge_configure(&c)==CMD_CONFIGURED,"Configure");ns[0]='X';name[0]='X';
    Check(dcp_cmd_bridge_configure(&c)==CMD_CONFIG_ALREADY_SET,"Immutable config");
}
void Cases(){
    for(unsigned op=0;op<2;++op){
        Reset(op);auto v=Call();Counts(1,1,1,CMD_COMPLETE);Check(v.value==R.result&&R.raised==0,"Legacy native result");
        Reset(op);R.result=nullptr;R.readBool=0;v=Call();Check(v.value==nullptr&&v.boolean==0,"Legacy null/false preserved");
        Reset(op);R.mode=CMD_HANDLED;v=Call();Counts(0,1,1,CMD_COMPLETE);Check(v.value==R.handled.value&&v.version==R.handled.version&&v.boolean==R.handled.boolean,"Handled result after cleanup");
        Reset(op);R.mode=CMD_HANDLED;R.zeroCookie=true;v=Call();Check(v.value==R.handled.value,"Zero cookie allowed");
        Reset(op);R.mode=CMD_REJECT;Fails("reject");Counts(0,0,1,CMD_PREPARE_REJECTED);Check(R.exactName,"Exception strings copied");if(op==1)Check(!R.command&&R.outVersion==0,"Rejection clears seeded outputs");
        Reset(op);R.mode=27;Fails("reject");Counts(0,0,1,CMD_INVALID_PLAN);
        Reset(op);R.invalid=true;Fails("reject");Counts(0,0,1,CMD_INVALID_PLAN);
        Reset(op);R.prepareThrow=true;Fails("reject");Counts(0,0,1,CMD_CALLBACK_EXCEPTION);
        Reset(op);R.mode=CMD_HANDLED;R.finishThrow=true;Fails("reject");Counts(0,1,1,CMD_CALLBACK_EXCEPTION);if(op==1)Check(!R.command&&R.outVersion==0,"Finish exception no publication");
        Reset(op);R.mode=CMD_HANDLED;R.finish=1;Fails("reject");Counts(0,1,1,CMD_FINISH_REJECTED);if(op==1)Check(!R.command&&R.outVersion==0,"Finish rejection no publication");
        Reset(op);R.mode=CMD_HANDLED;R.cleanup=1;Fails("reject");Counts(0,1,1,CMD_COMPLETE);if(op==1)Check(!R.command&&R.outVersion==0,"Cleanup failure no publication");
        Reset(op);R.mode=CMD_HANDLED;R.cleanupThrow=true;Fails("reject");Counts(0,1,1,CMD_COMPLETE);if(op==1)Check(!R.command&&R.outVersion==0,"Cleanup exception no publication");
        Reset(op);R.originalThrow=true;R.cleanupThrow=true;Fails("original");Counts(1,0,1,CMD_ORIGINAL_EXCEPTION);Check(R.raised==0,"Cleanup cannot mask native exception");if(op==1)Check(R.command==R.result&&R.outVersion==R.readVersion,"Legacy native failure output behavior preserved");
        Reset(op);R.finish=1;Fails("reject");Counts(1,1,1,CMD_FINISH_REJECTED);if(op==1)Check(R.command==R.result&&R.outVersion==R.readVersion,"PASS finish failure preserves raw outputs");
        Reset(op);R.cleanup=1;Fails("reject");Counts(1,1,1,CMD_COMPLETE);if(op==1)Check(R.command==R.result&&R.outVersion==R.readVersion,"PASS cleanup failure preserves raw outputs");
        Reset(op);R.nested=true;v=Call();Counts(1,1,1,CMD_COMPLETE);Check(v.value==R.result,"Nested handled read leaves outer identity/result intact");
    }
    for(auto ver:{std::numeric_limits<std::int32_t>::min(),-1,0,51,123,std::numeric_limits<std::int32_t>::max()}){Reset(0);R.version=ver;Call();Check(R.originals==1,"Signed native version preserved");}
    Reset(1);R.readBool=0xA5;Check(Call().boolean==0xA5,"Native UInt8 return not normalized");
    for(int missing=0;missing<3;++missing){Reset(1);if(missing!=1)R.expectedCommand=nullptr;if(missing!=0)R.expectedVersion=nullptr;Call();Counts(1,1,1,CMD_COMPLETE);}
    for(int missing=0;missing<2;++missing){Reset(1);R.mode=CMD_HANDLED;if(missing==0)R.expectedCommand=nullptr;else R.expectedVersion=nullptr;Fails("reject");Counts(0,0,1,CMD_INVALID_PLAN);}
    for(int invalid=0;invalid<4;++invalid){Reset(1);R.mode=CMD_HANDLED;if(invalid==0)R.handled.boolean=2;if(invalid==1)R.handled.value=nullptr;if(invalid==2)R.handled.boolean=0;if(invalid==3)R.handled={nullptr,123,0};Fails("reject");Counts(0,0,1,CMD_INVALID_PLAN);}
    Reset(1);R.mode=CMD_HANDLED;R.handled={nullptr,0,0};auto v=Call();Check(!v.value&&v.version==0&&v.boolean==0,"Handled false has defined empty outputs");
    Reset(0);R.mode=CMD_HANDLED;R.handled.value=nullptr;Fails("reject");Counts(0,0,1,CMD_INVALID_PLAN);
    Check(Depth==0,"No active callback frames");
}
}
int main(){try{Configuration();Cases();std::cout<<"PASS "<<Passed<<" typed command bridge checks\n";return 0;}catch(const std::exception& e){std::cerr<<"FAIL "<<e.what()<<"\n";return 1;}}
