#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <shellapi.h>
#include <algorithm>
#include <cwchar>
#include <limits>
#include <mutex>
#include <sstream>
#include <string>
#include <thread>

namespace {
constexpr DWORD fullscreen = CDS_FULLSCREEN;
struct ModeLease {
    std::mutex gate;
    std::wstring device;
    DWORD parentId{};
    DEVMODEW baseline{}, applied{};
    bool active{};

    static DEVMODEW EmptyMode() {
        DEVMODEW mode{};
        mode.dmSize = sizeof(mode);
        return mode;
    }
    bool ReadCurrent(DEVMODEW& mode) const {
        mode = EmptyMode();
        return EnumDisplaySettingsW(device.c_str(), ENUM_CURRENT_SETTINGS, &mode) != FALSE;
    }
    static bool SameMode(const DEVMODEW& a, const DEVMODEW& b) {
        return a.dmPelsWidth == b.dmPelsWidth && a.dmPelsHeight == b.dmPelsHeight &&
            a.dmBitsPerPel == b.dmBitsPerPel && a.dmDisplayFrequency == b.dmDisplayFrequency &&
            a.dmDisplayFlags == b.dmDisplayFlags && a.dmDisplayOrientation == b.dmDisplayOrientation &&
            a.dmDisplayFixedOutput == b.dmDisplayFixedOutput &&
            a.dmPosition.x == b.dmPosition.x && a.dmPosition.y == b.dmPosition.y;
    }
    bool ForegroundBelongsToParent() const {
        DWORD owner{};
        GetWindowThreadProcessId(GetForegroundWindow(), &owner);
        return owner == parentId;
    }
    LONG RestoreLocked() {
        if (!active) return DISP_CHANGE_SUCCESSFUL;
        DEVMODEW current;
        if (!ReadCurrent(current)) return DISP_CHANGE_FAILED;
        // A user/driver topology or mode change supersedes this lease. Never
        // overwrite a newer external display selection with an old snapshot.
        if (!SameMode(current, applied)) { active = false; return DISP_CHANGE_SUCCESSFUL; }
        LONG result = ChangeDisplaySettingsExW(device.c_str(), &baseline, nullptr, fullscreen, nullptr);
        if (result == DISP_CHANGE_SUCCESSFUL) active = false;
        return result;
    }
    LONG Restore() { std::lock_guard lock(gate); return RestoreLocked(); }
    LONG DesktopExtent(DWORD& width, DWORD& height) {
        std::lock_guard lock(gate);
        DEVMODEW current;
        if (!ReadCurrent(current)) return DISP_CHANGE_FAILED;
        if (active && !SameMode(current, applied)) active = false;
        const auto& desktop = active ? baseline : current;
        width = desktop.dmPelsWidth; height = desktop.dmPelsHeight;
        return DISP_CHANGE_SUCCESSFUL;
    }
    LONG Apply(DWORD width, DWORD height) {
        std::lock_guard lock(gate);
        if (width < 1280 || height < 720 || width > 32768 || height > 32768)
            return DISP_CHANGE_BADPARAM;
        if (!ForegroundBelongsToParent()) return DISP_CHANGE_BADPARAM;
        DEVMODEW current;
        if (!ReadCurrent(current)) return DISP_CHANGE_FAILED;
        if (active && !SameMode(current, applied)) active = false;
        if (!active) baseline = current;
        DEVMODEW choice{};
        unsigned bestDistance = std::numeric_limits<unsigned>::max();
        for (DWORD index = 0;; ++index) {
            auto mode = EmptyMode();
            if (!EnumDisplaySettingsW(device.c_str(), index, &mode)) break;
            if (mode.dmPelsWidth != width || mode.dmPelsHeight != height || mode.dmBitsPerPel != 32 ||
                (mode.dmDisplayFlags & DM_INTERLACED) != 0) continue;
            auto frequency = static_cast<int>(mode.dmDisplayFrequency);
            auto baseFrequency = static_cast<int>(baseline.dmDisplayFrequency);
            unsigned distance = static_cast<unsigned>(std::abs(frequency - baseFrequency));
            if (distance >= bestDistance) continue;
            choice = mode; bestDistance = distance;
        }
        if (bestDistance == std::numeric_limits<unsigned>::max()) return DISP_CHANGE_BADMODE;
        // Use the driver's real enumerated mode, without changing its topology
        // or writing HKCU/registry defaults. Rotation/fixed output are retained.
        choice.dmFields &= DM_BITSPERPEL | DM_PELSWIDTH | DM_PELSHEIGHT |
            DM_DISPLAYFLAGS | DM_DISPLAYFREQUENCY | DM_DISPLAYORIENTATION | DM_DISPLAYFIXEDOUTPUT;
        LONG result = ChangeDisplaySettingsExW(device.c_str(), &choice, nullptr, CDS_TEST, nullptr);
        if (result != DISP_CHANGE_SUCCESSFUL) return result;
        result = ChangeDisplaySettingsExW(device.c_str(), &choice, nullptr, fullscreen, nullptr);
        if (result != DISP_CHANGE_SUCCESSFUL) return result;
        // A successful call owns the changed output even if subsequent current
        // settings publication fails; retain a restore target in that case.
        applied = choice; applied.dmPosition = baseline.dmPosition; active = true;
        if (!ReadCurrent(current)) { RestoreLocked(); return DISP_CHANGE_FAILED; }
        applied = current;
        if (current.dmPelsWidth != width || current.dmPelsHeight != height) {
            RestoreLocked(); return DISP_CHANGE_BADMODE;
        }
        return DISP_CHANGE_SUCCESSFUL;
    }
    void SuspendIfBackground() {
        std::lock_guard lock(gate);
        if (active && !ForegroundBelongsToParent()) RestoreLocked();
    }
};

bool ReadLine(HANDLE input, std::string& text) {
    text.clear();
    char value; DWORD read{};
    while (ReadFile(input, &value, 1, &read, nullptr) && read == 1) {
        if (value == '\n') return true;
        if (value != '\r') text.push_back(value);
        if (text.size() > 80) return false;
    }
    return false;
}
bool Reply(HANDLE output, LONG result) {
    std::string text = std::to_string(result) + "\n";
    DWORD written{};
    return WriteFile(output, text.data(), static_cast<DWORD>(text.size()), &written, nullptr) && written == text.size();
}
bool ReplyExtent(HANDLE output, DWORD width, DWORD height) {
    std::string text = "0 " + std::to_string(width) + " " + std::to_string(height) + "\n";
    DWORD written{};
    return WriteFile(output, text.data(), static_cast<DWORD>(text.size()), &written, nullptr) && written == text.size();
}
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
    int count{};
    auto args = CommandLineToArgvW(GetCommandLineW(), &count);
    if (!args || count != 4) { if (args) LocalFree(args); return 2; }
    wchar_t* end{};
    auto parentId = wcstoul(args[1], &end, 10);
    if (!parentId || *end) { LocalFree(args); return 2; }
    auto rawWindow = _wcstoui64(args[2], &end, 16);
    if (!rawWindow || *end) { LocalFree(args); return 2; }
    HWND window = reinterpret_cast<HWND>(static_cast<UINT_PTR>(rawWindow));
    std::wstring device(args[3]); LocalFree(args);
    DWORD windowOwner{};
    GetWindowThreadProcessId(window, &windowOwner);
    if (windowOwner != parentId) return 3;
    MONITORINFOEXW monitor{}; monitor.cbSize = sizeof(monitor);
    if (!GetMonitorInfoW(MonitorFromWindow(window, MONITOR_DEFAULTTONEAREST), &monitor) ||
        device != monitor.szDevice) return 3;
    HANDLE parent = OpenProcess(SYNCHRONIZE, FALSE, parentId);
    if (!parent) return 3;
    std::wstring name = L"Local\\San14Remake.DisplayMode.";
    for (wchar_t c : device) name += (c >= L'0' && c <= L'9') ||
        (c >= L'A' && c <= L'Z') || (c >= L'a' && c <= L'z') ? c : L'_';
    HANDLE ownership = CreateMutexW(nullptr, TRUE, name.c_str());
    if (!ownership || GetLastError() == ERROR_ALREADY_EXISTS) {
        if (ownership) CloseHandle(ownership);
        CloseHandle(parent); return 4;
    }
    HANDLE input = GetStdHandle(STD_INPUT_HANDLE), output = GetStdHandle(STD_OUTPUT_HANDLE);
    ModeLease lease; lease.device = device; lease.parentId = parentId;
    // The one hidden child owns only this parent and monitor. The finite wait
    // also returns the desktop on Alt+Tab even if the managed UI is blocked.
    HANDLE finished = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!finished) { ReleaseMutex(ownership); CloseHandle(ownership); CloseHandle(parent); return 5; }
    std::thread watch([&] {
        HANDLE waits[] = { parent, finished };
        for (;;) {
            DWORD event = WaitForMultipleObjects(2, waits, FALSE, 100);
            if (event == WAIT_OBJECT_0) { lease.Restore(); ExitProcess(0); }
            if (event == WAIT_OBJECT_0 + 1) return;
            if (event == WAIT_TIMEOUT) lease.SuspendIfBackground();
            else { lease.Restore(); ExitProcess(6); }
        }
    });
    LONG exitCode = 0;
    if (Reply(output, 0)) {
        std::string line;
        while (ReadLine(input, line)) {
            std::istringstream command(line);
            char operation{}; command >> operation;
            LONG result = DISP_CHANGE_BADPARAM;
            bool quit = false;
            if (operation == 'A') {
                DWORD width{}, height{}; std::string extra;
                if ((command >> width >> height) && !(command >> extra)) result = lease.Apply(width, height);
            } else if (operation == 'D') {
                std::string extra;
                DWORD width{}, height{};
                if (!(command >> extra)) {
                    result = lease.DesktopExtent(width, height);
                    if (result == DISP_CHANGE_SUCCESSFUL) {
                        if (!ReplyExtent(output, width, height)) break;
                        continue;
                    }
                }
            } else if (operation == 'R' || operation == 'Q') {
                std::string extra;
                if (!(command >> extra)) { result = lease.Restore(); quit = operation == 'Q'; }
            }
            if (!Reply(output, result) || quit) { exitCode = result; break; }
        }
    }
    LONG restored = lease.Restore();
    if (restored != DISP_CHANGE_SUCCESSFUL) exitCode = restored;
    SetEvent(finished); watch.join();
    CloseHandle(finished); ReleaseMutex(ownership); CloseHandle(ownership); CloseHandle(parent);
    return static_cast<int>(exitCode);
}
