#:package YueYinqiu.Su.DotnetRunFileUtilities@0.0.3

using CliWrap;

// 约定命令行参数
var pythonScript = new FileInfo(args[0]);    // 要执行的脚本路径
var partition = args[1];    // 要使用的分区
var count = int.Parse(args[2]);    // 使用的 CPU 或者 GPU 数量（具体是 CPU 还是 GPU 按照分区判断）

var projectName = "sample_projct";
var sbatchScript = 
    $"""
    #!/bin/bash
    cd "{Environment.CurrentDirectory}"    # 这行不是必要的，一般来说会继承当前环境变量
    module load cuda/12.8
    uv run "{pythonScript.FullName}"
    """;
var cpusPerGpu = 7;
string? email = null;

var partitionDictionary = new Dictionary<string, string?>()
{
    { "intel", null },
    { "amd", null },
    { "L40", "l40" },
    { "A800", "a800" },
};
var partitionGpu = partitionDictionary[partition];

var scriptName = Path.GetFileNameWithoutExtension(pythonScript.Name);

var outputPath = new DirectoryInfo(Path.Join(
    Environment.CurrentDirectory,
    "links", "outputs", scriptName,
    DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff")));
outputPath.Create();

var sbatchScriptPath = Path.Join(outputPath.FullName, $"{scriptName}.sh");
File.WriteAllText(sbatchScriptPath, sbatchScript);

var arguments = new Dictionary<string, string?>
{
    // { "exclude", ... },

    { "partition", partition },
    { "nodes", "1" },
    { "ntasks-per-node", "1" },
    { "gres", partitionGpu is null ? null : $"gpu:{partitionGpu}:{count}" },
    { "gpus-per-task", partitionGpu is null ? null : $"{partitionGpu}:{count}" },
    { "cpus-per-task", partitionGpu is null ? $"{count}" : $"{count * cpusPerGpu}" },
    // { "mem-per-cpu", ... },
    // { "mem-per-gpu", ... },

    { "job-name", $"{projectName}_{scriptName}" },
    { "comment", pythonScript.FullName },
    { "output", Path.Join(outputPath.FullName, "%j.out") },
    { "error", Path.Join(outputPath.FullName, "%j.err") },
    { "mail-type", "ALL" },
    { "mail-user", email },
};

var command = Cli.Wrap("sbatch").WithArguments(arguments
    .Where(x => x.Value != null)
    .SelectMany(x => new[] { $"--{x.Key}", $"{x.Value}" })
    .Append(sbatchScriptPath));
await (command | (Console.WriteLine, Console.Error.WriteLine)).ExecuteAsync();
