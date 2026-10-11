using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class ProgrammedTaskData
{
    public string task { get; set; } = string.Empty;
    public List<string> arguments { get; set; } = new();
    public string timeout { get; set; } = "60s";
    public bool autostart { get; set; }
}


public static class ProgrammedTasks
{
    private static int ParseTimeout(string timeout)
    {
        if (string.IsNullOrWhiteSpace(timeout))
        {
            throw new ArgumentException("Invalid timeout format. Use 's', 'm', or 'h'.");
        }

        var normalizedTimeout = timeout.Trim();
        if (normalizedTimeout.Length <= 1 || normalizedTimeout[^1] is not ('s' or 'm' or 'h'))
        {
            throw new ArgumentException("Invalid timeout format. Use 's', 'm', or 'h'.");
        }

        if (!int.TryParse(normalizedTimeout[..^1], out var timeValue) || timeValue < 0)
        {
            throw new ArgumentException("Invalid timeout format. Use 's', 'm', or 'h'.");
        }

        return normalizedTimeout[^1] switch
        {
            's' => timeValue * 1000,
            'm' => timeValue * 60 * 1000,
            'h' => timeValue * 3600 * 1000,
            _ => throw new ArgumentException("Invalid timeout format. Use 's', 'm', or 'h'.")
        };
    }

    public static void ProgrammTask(string task, List<string> arguments, string timeout, bool autostart)
    {
        if (!Directory.Exists(Program.directoryPath))
        {
            Directory.CreateDirectory(Program.directoryPath);
        }

        Directory.SetCurrentDirectory(Program.directoryPath);

        ParseTimeout(timeout);

        var taskData = new ProgrammedTaskData
        {
            task = task,
            arguments = arguments,
            timeout = timeout,
            autostart = autostart
        };

        var json = JsonSerializer.Serialize(taskData, ProgrammedTaskDataContext.Default.ProgrammedTaskData);
        File.WriteAllText($"{task}.json", json);

        Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    public static void DeleteProgrammTask(string task)
    {
        if (!Directory.Exists(Program.directoryPath))
        {
            Directory.CreateDirectory(Program.directoryPath);
        }

        Directory.SetCurrentDirectory(Program.directoryPath);

        var filePath = $"{task}.json";
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    public static async Task GetProgrammTasks()
    {
        if (!Directory.Exists(Program.directoryPath))
        {
            Directory.CreateDirectory(Program.directoryPath);
        }

        Directory.SetCurrentDirectory(Program.directoryPath);

        var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.json");
        foreach (var file in files)
        {
            var json = await File.ReadAllTextAsync(file);
            var taskInfo = JsonSerializer.Deserialize(json, ProgrammedTaskDataContext.Default.ProgrammedTaskData);
            if (taskInfo != null && taskInfo.autostart)
            {
                _ = Task.Run(() => ExecuteProgrammTask(taskInfo.task));
            }
        }
        Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    public static async Task ExecuteProgrammTask(string task)
    {
        try
        {
            if (!Directory.Exists(Program.directoryPath))
            {
                Directory.CreateDirectory(Program.directoryPath);
            }

            Directory.SetCurrentDirectory(Program.directoryPath);

            var filePath = $"{task}.json";
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                var taskInfo = JsonSerializer.Deserialize(json, ProgrammedTaskDataContext.Default.ProgrammedTaskData);

                Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

                if (taskInfo != null)
                {
                    var command = taskInfo.task;
                    var arguments = taskInfo.arguments ?? new List<string>();
                    while (true)
                    {
                        Program.ExecuteCommand(command, string.Join(" ", arguments));
                        await Task.Delay(ParseTimeout(taskInfo.timeout));
                    }
                }
            }
        }
        catch (Exception)
        {
            Console.WriteLine("An error occurred while executing the program task.");
            Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            throw;
        }
    }
}