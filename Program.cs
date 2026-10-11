using System.Diagnostics;
using System.Runtime.InteropServices;

public static class Program
{
    [DllImport("libc", SetLastError = true)]
    private static extern int mkfifo(string pathname, uint mode);

    public static List<Process> processes = new List<Process>();
    public static readonly string pipePath = ".task";

    public static readonly string directoryPath = ".background-tasks";

    public static Dictionary<string, Action> commands = new()
    {
        {"-h" , Help},
        {"--help" , Help},
        {"--programm", ProgrammTaskInput},
        {"--delete-programm", ProgrammTaskDeleteInput},
        {"--kill-all", KillAll}

    };

    public static async Task Main(string[] args)
    {
        Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var path = $"/home/{Environment.UserName}/{pipePath}";
        if (!File.Exists(path))
        {
            mkfifo(path, 438);
        }

        try
        {
            if (args.Length > 0 && commands.ContainsKey(args[0]))
            {
                commands[args[0]].Invoke();
                return;
            }
            else if (args.Length > 0)
            {
                Console.WriteLine($"Unknown command: {args[0]}");
                return;
            }
            else if (args.Length == 0)
            {           
                Console.WriteLine("Initializing background tasks...");
                await ProgrammedTasks.GetProgrammTasks();

                Console.WriteLine("Listening for tasks... (use 'echo \"[command]\" > .task' to send a task)");

                using var stream = new FileStream(pipePath, FileMode.Open, FileAccess.Read);
                using var reader = new StreamReader(stream);

                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (line != null)
                    {
                        _ = Task.Run(() => Tasks(line));
                    }
                }
            }
            
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: {e.Message}");
        }  
        
    }

    public static async Task Tasks(string task)
    {
        try
        {         
            var command = task.Split()[0];
            if (command == "exec")
            {
                var commandArgs = task.Split()[1..];

                var process = ExecuteCommand(commandArgs[0], string.Join(" ", commandArgs[1..]));
                processes.Add(process);
            }

            if (command == "kill")
            {
                var arguments = task.Split()[1..].ToList();
                if (arguments.Count == 0)
                {
                    KillTasks();              
                }
                else
                {
                    KillTasks(arguments);
                }
            }

            if (command == "start-programm")
            {
                var taskName = task.Split()[1];
                _ = Task.Run(() => ProgrammedTasks.ExecuteProgrammTask(taskName));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error in Task: {e.Message}");
        }
    }

    public static void KillTasks(List<string>? task = null)
    {
        var processesToKill = processes;
        var killedProcesses = new List<Process>();
        try
        {
            if (task != null)
            {
                try
                {                   
                    processesToKill = processes.Where(p => {if (!p.HasExited) return task.Contains(p.ProcessName); return false; }).ToList();
                }
                catch
                {
                    
                }
            }

            foreach (var process in processesToKill)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }         
                    killedProcesses.Add(process);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error killing process {process.ProcessName}: {e.Message}");
                }               
            }

            foreach (var process in killedProcesses)
            {
                try
                {             
                    processes.Remove(process);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error removing process {process.ProcessName} from list: {e.Message}");
                }
            }

            killedProcesses.Clear();
            processesToKill.Clear();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error killing processes: {e.Message}");
        }
    }

    public static Process ExecuteCommand(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo()
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            return Process.Start(psi)!;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error executing command: {e.Message}");
            return null!;
        }
    }

    private static void Help()
    {
        Console.WriteLine("""
        usage:
        Init the application (Better without the Terminal)

        CommandLine args:
        --kill-all              - terminate all running background-tasks processes
        --programm              - programm a task
        --delete-programm       - delete a programm task

        Use the commands:
        use 'echo "[command] > .task"' for init a task

        Commands:
        exec [executable]       - execute the file
        kill [process name]     - kill all tasks with the same process name (kill all without parameter)
        start-programm [task]   - start a non-autostart programmed task
        """);
    }

    private static void ProgrammTaskInput()
    {
        Console.Write("Task File > ");
        var task = Console.ReadLine();

        Console.Write("Task Arguments (separated by space) > ");
        List<string> argumentsInput = Console.ReadLine()?.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()!;

        Console.Write("Autostart (y/n) > ");
        var autostartInput = Console.ReadLine() == "y" ? true : false;

        Console.Write("Timeout (s, m, h) > ");
        var timeoutInput = Console.ReadLine();

        ProgrammedTasks.ProgrammTask(task!, argumentsInput, timeoutInput!, autostartInput);
    }

    private static void ProgrammTaskDeleteInput()
    {
        Console.Write("Task File > ");
        var task = Console.ReadLine();

        ProgrammedTasks.DeleteProgrammTask(task!);
    }

    private static void KillAll()
    {
        KillTasks();

        var currentProcessId = Environment.ProcessId;
        var backgroundTasksProcesses = Process.GetProcessesByName("background-tasks")
            .OrderBy(process => process.Id == currentProcessId)
            .ToList();

        foreach (var process in backgroundTasksProcesses)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error killing background-tasks process {process.Id}: {e.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}