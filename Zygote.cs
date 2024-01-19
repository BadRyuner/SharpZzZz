using Reloaded.Assembler;
using Reloaded.Injector;
using Reloaded.Memory.Buffers;
using Reloaded.Memory.Sources;
using Reloaded.Memory.Utilities;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpZzZz;
internal class Zygote
{
    internal static void Create(string process) => Create_Internal(Process.GetProcessesByName(process)[0]);
    internal static void Create(Process process) => Create_Internal(process);
    internal static void Create(Func<Process> get) => Create_Internal(get());

    private static ExternalMemory _mem;
    private static PrivateMemoryBuffer _code;
    private static CircularBuffer _buf;
    private static Assembler _asm;
    private static Process _proc;

    static void Create_Internal(Process proc)
    {
        _proc = proc;
        _mem = new ExternalMemory(proc);
        _code = new MemoryBufferHelper(proc).CreatePrivateMemoryBuffer(1024);
        _buf = new CircularBuffer(1024*4, _mem);
        _asm = new Assembler();

        var shell = new Shellcode(proc);
        var hostfxr = shell.LoadLibraryW(GetLib("hostfxr.dll"));

        var assembly = typeof(Zygote).Assembly.Location;
        var context = InitForCLI(shell.GetProcAddress(hostfxr, "hostfxr_initialize_for_dotnet_command_line"), assembly);
        RunApp(shell.GetProcAddress(hostfxr, "hostfxr_run_app"), context);
    }

    static string GetLib(string lib)
    {
        var current = Process.GetCurrentProcess();
        var modules = current.Modules;
        foreach (ProcessModule module in modules)
        {
            if (module.ModuleName == lib)
                return module.FileName;
        }
        throw new Exception($"Cant get {lib}");
    }

    static void RunApp(long func, long ctx)
    {
        _buf.Offset = 0;

        var err = 0L;
        var err_ptr = _buf.Add(ref err);

        string[] test;
        var code = _asm.Assemble(test = new[] {
            "use64",
            "sub rsp, 72",
           $"mov rax, qword {func}",
           $"mov rcx, qword {ctx}",
           $"call rax",
           $"mov qword[qword {err_ptr}], rax",
            "add rsp, 72",
            "ret"
            });
        var alloced_code = _code.Add(code);

        WaitForSingleObject(CreateRemoteThread(_proc.Handle, IntPtr.Zero, 0, (IntPtr)alloced_code, 0, 0, out var _), uint.MaxValue);

        _mem.Read<long>(err_ptr, out err);
        ThrowError(err);
    }

    static long InitForCLI(long func, string dll)
    {
        _buf.Offset = 0;
        var dll_path = _buf.Add(Encoding.Unicode.GetBytes(dll + '\0'));

        var args = _buf.Add(ref dll_path);

        var ptr = 0L;
        var ctx = _buf.Add(ref ptr);
        var err = 0L;
        var err_ptr = _buf.Add(ref err);

        string[] test;
        var code = _asm.Assemble(test = new[] {
            "use64",
            "sub rsp, 72",
           $"mov rax, qword {func}",
            "mov rcx, 1",
           $"mov rdx, qword {args}",
            "mov r8, 0",
           $"mov r9, {ctx}",
           $"call rax",
           $"mov qword[qword {err_ptr}], rax",
            "add rsp, 72",
            "ret"
            });
        var alloced_code = _code.Add(code);

        WaitForSingleObject(CreateRemoteThread(_proc.Handle, IntPtr.Zero, 0, (IntPtr)alloced_code, 0, 0, out var _), uint.MaxValue);

        _mem.Read<long>(ctx, out ptr);

        if (ptr == 0)
        {
            _mem.Read<long>(err_ptr, out err);
            ThrowError(err);
        }

        return ptr;
    }

    static void ThrowError(long code)
    {
        switch(code)
        {
            case 0:
            case 1:
            case 2:
                return;
            case 0x80008081: throw new Exception("InvalidArgFailure");
            case 0x80008082: throw new Exception("CoreHostLibLoadFailure");
            case 0x80008083: throw new Exception("CoreHostLibMissingFailure");
            case 0x80008084: throw new Exception("CoreHostEntryPointFailure");
            case 0x80008085: throw new Exception("CoreHostCurHostFindFailure");
            case 0x80008087: throw new Exception("CoreClrResolveFailure");
            case 0x80008088: throw new Exception("CoreClrBindFailure");
            case 0x80008089: throw new Exception("CoreClrInitFailure");
            case 0x8000808a: throw new Exception("CoreClrExeFailure");
            case 0x8000808b: throw new Exception("ResolverInitFailure");
            case 0x8000808c: throw new Exception("ResolverResolveFailure");
            case 0x8000808d: throw new Exception("LibHostCurExeFindFailure");
            case 0x8000808e: throw new Exception("LibHostInitFailure");
            case 0x80008090: throw new Exception("LibHostExecModeFailure");
            case 0x80008091: throw new Exception("LibHostSdkFindFailure");
            case 0x80008092: throw new Exception("LibHostInvalidArgs");
            case 0x80008093: throw new Exception("InvalidConfigFile");
            case 0x80008094: throw new Exception("AppArgNotRunnable");
            case 0x80008095: throw new Exception("AppHostExeNotBoundFailure");
            case 0x80008096: throw new Exception("FrameworkMissingFailure");
            case 0x80008097: throw new Exception("HostApiFailed");
            case 0x80008098: throw new Exception("HostApiBufferTooSmall");
            case 0x80008099: throw new Exception("LibHostUnknownCommand");
            case 0x8000809a: throw new Exception("LibHostAppRootFindFailure");
            case 0x8000809b: throw new Exception("SdkResolverResolveFailure");
            case 0x8000809c: throw new Exception("FrameworkCompatFailure");
            case 0x8000809d: throw new Exception("FrameworkCompatRetry");
            case 0x8000809e: throw new Exception("AppHostExeNotBundle");
            case 0x8000809f: throw new Exception("BundleExtractionFailure");
            case 0x800080a0: throw new Exception("BundleExtractionIOError");
            case 0x800080a1: throw new Exception("LibHostDuplicateProperty");
            case 0x800080a2: throw new Exception("HostApiUnsupportedVersion");
            case 0x800080a3: throw new Exception("HostInvalidState");
            case 0x800080a4: throw new Exception("HostPropertyNotFound");
            case 0x800080a5: throw new Exception("CoreHostIncompatibleConfig");
            case 0x800080a6: throw new Exception("HostApiUnsupportedScenario");
            case 0x800080a7: throw new Exception("HostFeatureDisabled");
            default:
                throw new Exception($"Unknown code err: {code.ToString("x2")}");
        }
    }

    [DllImport("kernel32")]
    public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize,
        IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);
}
