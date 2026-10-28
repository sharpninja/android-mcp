using System.Globalization;
using System.Text;
using System.Text.Json;
namespace AndroidMcp.BuildTool.Core;
// Internal C# integration fixture in this same monolithic executable. No shell.
internal static class ProcessContractProbe
{
    public static int Run(string[] arguments)
    {
        var exit=int.Parse(arguments[0],CultureInfo.InvariantCulture);
        if(arguments[1]=="copy")
        {
            Console.OpenStandardInput().CopyTo(Console.OpenStandardOutput());
            Console.OpenStandardError().Write(Encoding.UTF8.GetBytes("stderr:\0雪/no-final-newline"));
        }
        else if(arguments[1]=="wait-file")
        {
            var gate=Path.GetFullPath(arguments[2]);
            Console.OpenStandardOutput().Write("ready"u8);Console.Out.Flush();
            while(!File.Exists(gate))Thread.Sleep(10);
        }
        else Console.OpenStandardOutput().Write(JsonSerializer.SerializeToUtf8Bytes(arguments.Skip(2).ToArray()));
        return exit;
    }
}
