// Minimal fixed-pixel Win32/DXGI presentation for the native experiment.
// Final109 already contains sRGB-encoded bytes. Copy them; do not shade again.
#pragma once
#include <dxgi1_2.h>
#include <vector>

struct NativePresentation {
    struct WheelEvent {int delta,clientX,clientY;};
    HWND window{};
    ComPtr<IDXGISwapChain1> chain;
    ComPtr<ID3D11Texture2D> backbuffer;
    bool paused=false;
    bool cameraControls=false;
    double focusDeltaX=0,focusDeltaZ=0;
    bool resetCamera=false;
    std::vector<WheelEvent> wheelEvents;
};
static LRESULT CALLBACK nativePresentationMessages(HWND window,UINT message,WPARAM w,LPARAM l) {
    if(message==WM_CLOSE||(message==WM_KEYDOWN&&w==VK_ESCAPE)){DestroyWindow(window);return 0;}
    if(message==WM_DESTROY){PostQuitMessage(0);return 0;}
    return DefWindowProcW(window,message,w,l);
}
static NativePresentation prepareNativePresentation(ID3D11Device* device,UINT width,UINT height,bool cameraControls=false) {
    NativePresentation result;result.cameraControls=cameraControls;
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    WNDCLASSW type{};type.lpfnWndProc=nativePresentationMessages;type.hInstance=GetModuleHandleW(nullptr);
    type.lpszClassName=L"San14NativeD3D11Experiment";type.hCursor=LoadCursorW(nullptr,MAKEINTRESOURCEW(32512)); // IDC_ARROW
    if(!RegisterClassW(&type))throw std::runtime_error("Native presentation window class failed");
    const DWORD style=WS_OVERLAPPEDWINDOW&~(WS_THICKFRAME|WS_MAXIMIZEBOX);
    result.window=CreateWindowExW(0,type.lpszClassName,cameraControls?L"San14 native D3D11 - camera (wheel/arrows/Home)":L"San14 native D3D11 - fixed camera",style,
        0,0,int(width),int(height),nullptr,nullptr,type.hInstance,nullptr);
    if(!result.window)throw std::runtime_error("Native presentation window failed");
    // Derive actual DPI-aware nonclient pixels; source render dimensions stay fixed.
    RECT outer{},client{};GetWindowRect(result.window,&outer);GetClientRect(result.window,&client);
    const int outerWidth=int(width)+(outer.right-outer.left)-(client.right-client.left);
    const int outerHeight=int(height)+(outer.bottom-outer.top)-(client.bottom-client.top);
    SetWindowPos(result.window,nullptr,std::max(0,(GetSystemMetrics(SM_CXSCREEN)-outerWidth)/2),
        std::max(0,(GetSystemMetrics(SM_CYSCREEN)-outerHeight)/2),outerWidth,outerHeight,SWP_NOZORDER|SWP_NOACTIVATE);
    GetClientRect(result.window,&client);
    if(client.right!=LONG(width)||client.bottom!=LONG(height))throw std::runtime_error("Native presentation client pixels differ from original render dimensions");
    ComPtr<IDXGIDevice> dxgi;check(device->QueryInterface(IID_PPV_ARGS(&dxgi)),"Native presentation DXGI device");
    ComPtr<IDXGIAdapter> adapter;check(dxgi->GetAdapter(&adapter),"Native presentation adapter");
    ComPtr<IDXGIFactory2> factory;check(adapter->GetParent(IID_PPV_ARGS(&factory)),"Native presentation factory");
    DXGI_SWAP_CHAIN_DESC1 description{};description.Width=width;description.Height=height;
    description.Format=DXGI_FORMAT_B8G8R8A8_UNORM;description.SampleDesc.Count=1;description.BufferUsage=DXGI_USAGE_RENDER_TARGET_OUTPUT;
    description.BufferCount=2;description.Scaling=DXGI_SCALING_STRETCH;description.SwapEffect=DXGI_SWAP_EFFECT_FLIP_DISCARD;
    description.AlphaMode=DXGI_ALPHA_MODE_IGNORE;
    check(factory->CreateSwapChainForHwnd(device,result.window,&description,nullptr,nullptr,&result.chain),"Native presentation swap chain");
    check(factory->MakeWindowAssociation(result.window,DXGI_MWA_NO_ALT_ENTER),"Native presentation fixed window mode");
    check(result.chain->GetBuffer(0,IID_PPV_ARGS(&result.backbuffer)),"Native presentation backbuffer");
    return result;
}
static bool pumpNativePresentation(NativePresentation& presentation) {
    MSG message{};
    while(PeekMessageW(&message,nullptr,0,0,PM_REMOVE)) {
        if(message.message==WM_QUIT)return false;
        if(message.hwnd==presentation.window&&message.message==WM_KEYDOWN) {
            if(message.wParam==VK_SPACE&&(message.lParam&0x40000000)==0)presentation.paused=!presentation.paused;
            if(presentation.cameraControls) {
                // Experiment controls publish caller focus requests in world X/Z.
                // Each accepted request still uses the original controller math.
                if(message.wParam==VK_LEFT)presentation.focusDeltaX-=1000;
                if(message.wParam==VK_RIGHT)presentation.focusDeltaX+=1000;
                if(message.wParam==VK_UP)presentation.focusDeltaZ-=1000;
                if(message.wParam==VK_DOWN)presentation.focusDeltaZ+=1000;
                if(message.wParam==VK_HOME){presentation.resetCamera=true;presentation.focusDeltaX=presentation.focusDeltaZ=0;}
            }
        }
        if(message.hwnd==presentation.window&&message.message==WM_MOUSEWHEEL&&presentation.cameraControls){
            POINT point{short(LOWORD(message.lParam)),short(HIWORD(message.lParam))};
            if(ScreenToClient(presentation.window,&point))presentation.wheelEvents.push_back({short(HIWORD(message.wParam)),point.x,point.y});
        }
        TranslateMessage(&message);DispatchMessageW(&message);
    }
    return IsWindow(presentation.window)!=FALSE;
}
static HRESULT presentNativeFinal(ID3D11DeviceContext* context,NativePresentation& presentation,ID3D11Texture2D* final) {
    const auto parent=NativeMainTargetScope::retain(context);
    context->OMSetRenderTargets(0,nullptr,nullptr);
    // Source90 and destination87 belong to the same BGRA typeless group.
    context->CopyResource(presentation.backbuffer.Get(),final);
    const HRESULT status=presentation.chain->Present(1,0);
    parent.restore(context);
    check(status,"Native presentation Present");return status;
}
