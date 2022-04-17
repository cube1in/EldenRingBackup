using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using static System.IO.File;

namespace EldenRingBackup;

internal static class Program
{
    /// <summary>
    /// 默认目录
    /// </summary>
    private static string _path =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EldenRing");

    /// <summary>
    /// 保存间隔
    /// </summary>
    private static Interval _interval = Interval.Hour;

    /// <summary>
    /// 保存记录限制
    /// </summary>
    private static int _limit = 10;

    /// <summary>
    /// 主方法
    /// </summary>
    /// <param name="args">可选参数:
    /// 埃尔登登法环文件夹路径，如C:\Users\default\AppData\Roaming\EldenRing
    /// 保存间隔，可选 Hour/Day
    /// 保存记录限制，如20
    /// 参数之间使用空格隔开，顺序不可更改
    /// <example>C:\Users\default\AppData\Roaming\EldenRing Day</example>
    /// <example>C:\Users\default\AppData\Roaming\EldenRing Day 20</example>
    /// </param>
    static void Main(string[] args)
    {
        // 控制台编码
        Console.OutputEncoding = Encoding.Unicode;

        if (args.Length > 0) _path = args[0];
        if (args.Length > 1 && Enum.TryParse<Interval>(args[2], out var interval)) _interval = interval;
        if (args.Length > 2 && int.TryParse(args[1], out var limit)) _limit = limit;

        if (!Directory.Exists(_path)) Console.WriteLine(@$"""{_path}"" directory does not exist");

        var watcher = new FileSystemWatcher(_path);
        watcher.Changed += OnChanged;
        watcher.Deleted += OnDeleted;
        watcher.EnableRaisingEvents = true;
        watcher.NotifyFilter = NotifyFilters.LastWrite;
        watcher.IncludeSubdirectories = true;

        Console.WriteLine(@$"文件夹：""{_path}"" 的监控已经开启...");
        Console.WriteLine("任意键退出监控...");

        Console.ReadKey();
    }

    /// <summary>
    /// 文件夹内发生删除时触发
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnDeleted(object sender, FileSystemEventArgs e)
    {
        // 判断是否是文件夹被删除（其余更改不监控）
        if (!GetAttributes(e.FullPath).HasFlag(FileAttributes.Directory)) return;

        Console.WriteLine(Environment.NewLine);
        Console.WriteLine(DateTime.Now);
        Console.WriteLine(@$"SteamID：{e.Name} 的存档目录已被删除，关于它的所有备份将进行删除...");

        // 删除所有备份
        foreach (var file in Directory.GetFiles(_path).Where(f => f.Contains(e.FullPath)))
        {
            Delete(file);
            Console.WriteLine($"文件：{file} 已删除...");
        }
    }

    /// <summary>
    /// 文件夹内发生改变时触发
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void OnChanged(object sender, FileSystemEventArgs e)
    {
        // 判断是否是文件夹更改（其余更改不监控）
        if (!GetAttributes(e.FullPath).HasFlag(FileAttributes.Directory)) return;

        var time = _interval switch
        {
            Interval.Hour => DateTime.Now.ToString("yyyy-MM-dd-HH"),
            Interval.Day => DateTime.Now.ToString("yyyy-MM-dd") + "-00",
            _ => throw new NotSupportedException()
        };

        var zip = e.FullPath + $"_{time}.zip";

        // 同一分钟内，如果还有更新，那么删除旧的
        if (Exists(zip)) Delete(zip);

        // 新增备份
        ZipFile.CreateFromDirectory(e.FullPath, zip);

        Console.WriteLine(Environment.NewLine);
        Console.WriteLine(DateTime.Now);
        Console.WriteLine(@$"SteamID：{e.Name} 的存档已经改变，已备份到：{zip}");

        var files = Directory.GetFiles(_path).Where(f => f.Contains(e.FullPath)).ToList();
        if (files.Count <= _limit) return;

        // 是否大于 limit 条记录，若是，删除最晚的那条
        var earliest = files.OrderByDescending(GetCreationTime).First();
        Delete(earliest);

        Console.WriteLine(Environment.NewLine);
        Console.WriteLine(DateTime.Now);
        Console.WriteLine(@$"SteamID：{e.Name} 的存档备份已经超过：{_limit} 条，删除最晚一条：{earliest}");
    }

    private enum Interval
    {
        Hour,
        Day
    }
}