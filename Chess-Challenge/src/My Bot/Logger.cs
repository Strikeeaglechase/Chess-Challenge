using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

public class Logger
{
    public static string logPath = Path.GetFullPath("../../../../log.txt");
    private static StreamWriter logWriter;

    public static void Init()
    {
        // logWriter = File.CreateText(logPath);
        // logWriter.AutoFlush = true;
    }

    public static void Log(string message)
    {
        // Console.WriteLine(message);
        // logWriter.WriteLine(message);
    }
}
