namespace AndroidMcp.BuildTool.Core;

/// <summary>Portable public command result; detailed native failures remain in retained receipts.</summary>
public static class NukeExitCode
{
    public static int Normalize(int engineCode) => engineCode == 0 ? 0 : 1;
}
