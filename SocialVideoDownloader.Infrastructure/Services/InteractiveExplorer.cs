using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SocialVideoDownloader.Core.Exceptions;

namespace SocialVideoDownloader.Infrastructure.Services;

internal static class InteractiveExplorer
{
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint TokenAccess =
        0x0001 | // TOKEN_ASSIGN_PRIMARY
        0x0002 | // TOKEN_DUPLICATE
        0x0008 | // TOKEN_QUERY
        0x0080 | // TOKEN_ADJUST_DEFAULT
        0x0100;  // TOKEN_ADJUST_SESSIONID

    public static void Start(params string[] arguments)
    {
        if (!OperatingSystem.IsWindows() || !IsServiceSession())
        {
            StartHere(arguments);
            return;
        }

        StartInActiveSession(arguments);
    }

    private static void StartHere(string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        Process.Start(startInfo);
    }

    private static void StartInActiveSession(string[] arguments)
    {
        var sessionId = FindActiveSession();
        if (sessionId == 0 || sessionId == 0xFFFFFFFF)
            throw new DownloadException("Açık bir kullanıcı oturumu bulunamadı.");

        if (!WTSQueryUserToken(sessionId, out var userToken))
            throw new DownloadException("Klasör bu oturumda açılamadı.");

        try
        {
            var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>() };
            if (!DuplicateTokenEx(userToken, TokenAccess, ref security, 2, 1, out var primary))
                throw new DownloadException("Klasör bu oturumda açılamadı.");

            try
            {
                if (!CreateEnvironmentBlock(out var environment, primary, false))
                    environment = IntPtr.Zero;

                try
                {
                    var startup = new StartupInfo
                    {
                        Cb = Marshal.SizeOf<StartupInfo>(),
                        Desktop = "winsta0\\default",
                    };
                    var command = BuildCommand(arguments);
                    if (!CreateProcessAsUser(
                            primary,
                            null,
                            command,
                            ref security,
                            ref security,
                            false,
                            CreateUnicodeEnvironment,
                            environment,
                            null,
                            ref startup,
                            out var process))
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    CloseHandle(process.Process);
                    CloseHandle(process.Thread);
                }
                finally
                {
                    if (environment != IntPtr.Zero)
                        DestroyEnvironmentBlock(environment);
                }
            }
            finally
            {
                CloseHandle(primary);
            }
        }
        catch (Win32Exception)
        {
            throw new DownloadException("Klasör bu oturumda açılamadı.");
        }
        finally
        {
            CloseHandle(userToken);
        }
    }

    private static string BuildCommand(string[] arguments)
    {
        var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        var parts = new List<string> { Quote(explorer) };
        foreach (var argument in arguments)
            parts.Add(argument.StartsWith("/select,", StringComparison.Ordinal) ? argument : Quote(argument));
        return string.Join(' ', parts);
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static bool IsServiceSession()
    {
        return ProcessIdToSessionId((uint)Environment.ProcessId, out var session) && session == 0;
    }

    private static uint FindActiveSession()
    {
        var console = WTSGetActiveConsoleSessionId();
        if (!WTSEnumerateSessions(IntPtr.Zero, 0, 1, out var buffer, out var count) || buffer == IntPtr.Zero)
            return console;

        try
        {
            var size = Marshal.SizeOf<SessionInfo>();
            uint? active = null;
            for (var index = 0; index < count; index++)
            {
                var info = Marshal.PtrToStructure<SessionInfo>(buffer + (index * size));
                if (info.State != 0 || info.SessionId == 0)
                    continue;

                if (info.SessionId == console)
                    return info.SessionId;

                active ??= info.SessionId;
            }

            return active ?? console;
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Cb;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2;
        public IntPtr Reserved2Pointer;
        public IntPtr StdInput;
        public IntPtr StdOutput;
        public IntPtr StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public int ProcessId;
        public int ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SessionInfo
    {
        public uint SessionId;
        public IntPtr StationName;
        public int State;
    }

    [DllImport("kernel32.dll")]
    private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSEnumerateSessions(IntPtr server, int reserved, int version, out IntPtr sessionInfo, out int count);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr existingToken,
        uint desiredAccess,
        ref SecurityAttributes tokenAttributes,
        int impersonationLevel,
        int tokenType,
        out IntPtr newToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, bool inherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(
        IntPtr token,
        string? applicationName,
        string commandLine,
        ref SecurityAttributes processAttributes,
        ref SecurityAttributes threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
