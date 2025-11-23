using System;
using System.IO;

namespace Downloader.Utils;

public abstract class Logger
{
    
    private static StreamWriter? _logWriter;

    public static void Init()
    {
        _logWriter = new StreamWriter(
            new FileStream("downloader.log", FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }
    
    private static readonly object Lock = new object();
    
    public static void Log(string message)
    {
        lock (Lock)
        {
            Console.WriteLine(message);
            _logWriter?.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}]: {message}");
        }
    }
    
}