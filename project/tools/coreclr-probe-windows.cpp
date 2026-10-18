// Isolated test host for the game's existing x64 CoreCLR. No game assemblies.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <filesystem>
#include <iostream>
#include <string>
static std::string utf8(const std::wstring& value) {
    int n=WideCharToMultiByte(CP_UTF8,0,value.c_str(),-1,nullptr,0,nullptr,nullptr);
    if(n<=0)throw std::runtime_error("UTF8 conversion failed");
    std::string out(static_cast<size_t>(n),'\0');
    if(!WideCharToMultiByte(CP_UTF8,0,value.c_str(),-1,out.data(),n,nullptr,nullptr))throw std::runtime_error("UTF8 conversion failed");
    out.pop_back();return out;
}
int wmain(int argc,wchar_t** argv) {
    if(argc!=6)return 2;
    try {
        std::filesystem::path runtime=std::filesystem::absolute(argv[1]),managed=std::filesystem::absolute(argv[2]);
        HMODULE clr=LoadLibraryExW((runtime/L"coreclr.dll").c_str(),nullptr,LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR|LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        if(!clr){std::cerr<<"Load CoreCLR failed "<<GetLastError()<<'\n';return 3;}
        using Initialize=int (__stdcall *)(const char*,const char*,int,const char**,const char**,void**,unsigned int*);
        using Delegate=int (__stdcall *)(void*,unsigned int,const char*,const char*,const char*,void**);
        using Shutdown=int (__stdcall *)(void*,unsigned int,int*);
        auto initialize=reinterpret_cast<Initialize>(GetProcAddress(clr,"coreclr_initialize"));
        auto delegate=reinterpret_cast<Delegate>(GetProcAddress(clr,"coreclr_create_delegate"));
        auto shutdown=reinterpret_cast<Shutdown>(GetProcAddress(clr,"coreclr_shutdown_2"));
        if(!initialize||!delegate||!shutdown)return 4;
        std::string tpa;
        for(const auto& entry:std::filesystem::directory_iterator(runtime))
            if(entry.is_regular_file()&&entry.path().extension()==L".dll")tpa+=utf8(entry.path().wstring())+';';
        if(tpa.empty())return 5;tpa.pop_back();
        std::string app=utf8(managed.wstring()),native=utf8(runtime.wstring())+';'+app,exe=utf8(argv[0]);
        const char* keys[]={"TRUSTED_PLATFORM_ASSEMBLIES","APP_PATHS","APP_NI_PATHS","NATIVE_DLL_SEARCH_DIRECTORIES"};
        const char* values[]={tpa.c_str(),app.c_str(),app.c_str(),native.c_str()};
        void* host=nullptr;unsigned int domain=0;
        int hr=initialize(exe.c_str(),"deception-windows-isolated-tests",4,keys,values,&host,&domain);
        if(hr<0){std::cerr<<"Initialize failed "<<std::hex<<hr<<'\n';return 6;}
        std::string assembly=utf8(argv[3]),type=utf8(argv[4]),method=utf8(argv[5]);
        void* entry=nullptr;hr=delegate(host,domain,assembly.c_str(),type.c_str(),method.c_str(),&entry);
        if(hr<0||!entry){std::cerr<<"Delegate failed "<<std::hex<<hr<<'\n';return 7;}
        int result=reinterpret_cast<int (__stdcall *)()>(entry)();
        int latched=0;hr=shutdown(host,domain,&latched);
        return hr<0?8:result;
    }catch(const std::exception& error){std::cerr<<error.what()<<'\n';return 9;}
}
