using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

using Gapotchenko.FX.Diagnostics;

class Program
{
    const uint PROCESS_ALL_ACCESS = 0x1F0FFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

    [DllImport("user32.dll")]
    static extern bool SetWindowText(IntPtr hWnd, string text);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    

    
    static extern bool CloseHandle(IntPtr hObject);

    static string[] envs = new string[5] { "JX_ACCESS_TOKEN", "JX_CHARACTER_ID", "JX_DISPLAY_NAME", "JX_REFRESH_TOKEN", "JX_SESSION_ID" };
    static string userpath = System.Environment.GetEnvironmentVariable("USERPROFILE");
    static string dllpath = Path.GetFullPath(userpath + "\\Documents\\MemoryError\\MemoryError.dll");
    static string basecmd = "C:\\ProgramData\\Jagex\\launcher\\rs2client.exe \"37\" \"content.runescape.com\" \"2\" \"1113\" \"27\" \"11\" \"41\" \"43594\" \"47\" \"43594\" \"49\" \"content.runescape.com\"";

    static void Main(string[] args)
    {

        string name = null;
        bool dorefresh = false;

        foreach (string arg in args)
        {
            if (arg.StartsWith("--name="))
            {
                name = arg.Substring("--name=".Length);
            }
            else if (arg == "--refresh")
            {
                dorefresh = true;
            }
        }

        if (string.IsNullOrEmpty(name))
        {
            Console.WriteLine("Error: The --name argument is mandatory.");
            return;
        }

        Console.WriteLine($"Name: {name}");
        Console.WriteLine($"Refresh: {dorefresh}");

        if (dorefresh)
        {
            refresh(name);
            standardLoad(name);
        }
        else
        {
            standardLoad(name);
            
        } 
    }

    static void standardLoad(string name)
    {
        Console.WriteLine("=== Standard Launcher ===");
        var x = loadEnvs(name);
        setEnvs(x);
        var y = loadCMD(name);
        Process c = launchClient(y);
        Thread.Sleep(7000);
        var a = GetPidsByName("rs2client")[0];
        InjectDll(a, dllpath);
    }

    static void refresh(string name)
    {
        Console.WriteLine("=== Launching Full Refresh ===");
        var pids = GetPidsByName("rs2client");
        Console.WriteLine($"found {pids.Count()} rs2clients");

        if (pids.Count() == 0) 
        {
            Console.WriteLine("No existing clients open to pull variables from, exiting.");
            Environment.Exit(1);
        }

        for (int i = 0; i < pids.Length; i++)
        {
            var p = pids[i];
            var e = getEnvs(p);
            setEnvs(e);
            saveEnvs(e);
            var x = loadEnvs(name);
            Console.WriteLine(x.Count());
            killProcesses("rs2client");
            Thread.Sleep(5000);
            var client = launchClient(basecmd);
            Thread.Sleep(10000);
            var newcmd = getCommandLineArgs("rs2client.exe");
            saveCMD(newcmd, name);
            killProcesses("rs2client");
            Thread.Sleep(5000);
        }
    }

    static string getCommandLineArgs(string procName)
    {
        Console.WriteLine("Tring to get cmd args");
        string wmiQuery = $"select CommandLine from Win32_Process where Name='{procName}'";
        Console.WriteLine(wmiQuery);
        ManagementObjectSearcher searcher = new ManagementObjectSearcher(wmiQuery);
        ManagementObjectCollection retObjectCollection = searcher.Get();
        Console.WriteLine($"Found {retObjectCollection.Count} objs");
        var launchercmd = "";
        foreach (ManagementObject retObject in retObjectCollection)
        {
           if (retObject["CommandLine"].ToString().Contains(" launcher "))
            {
                launchercmd = retObject["CommandLine"].ToString();
            }
        }
        Console.WriteLine($"Found launcher cmd: {launchercmd.Split(" launcher")[0]}");
        return launchercmd.Split(" launcher")[0];    
    }

    static void saveCMD(string cmd, string name)
    {
        Console.WriteLine($"Saving command for {name}");
        File.WriteAllText(Path.GetFullPath(userpath + $"\\{name}.cmd"), cmd);
    }

    static string loadCMD(string name)
    {
        Console.WriteLine($"Loading existing CMD for {name}");
        var text = File.ReadAllText(Path.GetFullPath(userpath + $"\\{name}.cmd"));
        return text;
    }

    static Process launchClient(string cmd)
    {
        string fp = cmd.Split(' ', 2)[0];
        string arg = cmd.Split(' ', 2)[1];
        ProcessStartInfo si = new ProcessStartInfo();
        si.UseShellExecute = true;
        si.FileName = fp;
        si.Arguments = arg;
        Console.WriteLine("Attempting to launch client at " + fp);
        var x = Process.Start(si);
        return x;
    }

    static int[] GetPidsByName(string processName)
    {
        Process[] processes = Process.GetProcessesByName(processName);
        int[] pids = new int[processes.Length];

        for (int i = 0; i < processes.Length; i++)
        {
            pids[i] = processes[i].Id;
        }

        return pids;
    }

    static void killProcesses(string processName)
    {
        Process[] processes = Process.GetProcessesByName(processName);
        for (int i = 0; i < processes.Length;i++)
        {
            Console.WriteLine("Killing RS client");
            processes[i].Kill();
        }
    }

    static Dictionary<string, string> getEnvs(int pid)
    {
        var process = Process.GetProcessById(pid);
        var env = process.ReadEnvironmentVariables();
        Dictionary<string, string> dict = new Dictionary<string, string>();

        for (int i = 0; i < envs.Length; i++)
        {
            Console.WriteLine(envs[i] + " : " + env[envs[i]]);
            dict.Add(envs[i], env[envs[i]]);
        }

        return dict;
    }

    static void saveEnvs(Dictionary<string, string> envs)
    {
        var name = envs["JX_DISPLAY_NAME"];
        var s = System.Text.Json.JsonSerializer.Serialize(envs);

        Console.WriteLine($"Saving env vars for {name}");
        File.WriteAllText(Path.GetFullPath(userpath + $"\\{name}.txt"), s);
    }

    static Dictionary<string, string> loadEnvs(string name)
    {
        Console.WriteLine($"Loading existing ENVs for {name}");
        var text = File.ReadAllText(Path.GetFullPath(userpath + $"\\{name}.txt"));
        Console.WriteLine($"Found {text}");
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(text);
    }

    static void setEnvs(Dictionary<string, string> envs)
    {
        Console.WriteLine("Attempting to set variables");
        foreach (var en in envs)
        {
            Console.WriteLine($"Setting environment variable: {en.Key} - {en.Value}");
            System.Environment.SetEnvironmentVariable(en.Key, en.Value);
        }
        
    }

    static void InjectDll(int targetPid, string dllPath)
    {
        IntPtr processHandle = OpenProcess(PROCESS_ALL_ACCESS, false, targetPid);

        if (processHandle == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to open process {targetPid}");
            return;
        }

        IntPtr loadLibraryAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");

        IntPtr dllPathAddress = VirtualAllocEx(processHandle, IntPtr.Zero, (uint)dllPath.Length, 0x3000, 0x40);

        byte[] buffer = System.Text.Encoding.ASCII.GetBytes(dllPath);

        int bytesWritten;
        WriteProcessMemory(processHandle, dllPathAddress, buffer, (uint)buffer.Length, out bytesWritten);

        IntPtr threadId = CreateRemoteThread(processHandle, IntPtr.Zero, 0, loadLibraryAddress, dllPathAddress, 0, IntPtr.Zero);

        if (threadId == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create remote thread in process {targetPid}");
        }
        else
        {
            Console.WriteLine($"DLL '{dllPath}' injected into process {targetPid}");
        }

        CloseHandle(processHandle);
    }
}