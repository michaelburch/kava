namespace Kava.Desktop;

public static class CrashLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Kava", "crash.log");

    public static void Write(string message, Exception? ex = null)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            if (ex != null)
                entry += $"\n  {ex.GetType().Name}: {ex.Message}\n  {ex.StackTrace}";
            entry += "\n";

            File.AppendAllText(LogPath, entry);
        }
        catch { /* don't crash the crash logger */ }
    }

    public static string FilePath => LogPath;
}
