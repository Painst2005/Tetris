using System.Threading;

namespace Tetris;

internal static class Program
{
    private static int _reportingException;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += OnThreadException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            Application.Run(new UI.MainForm());
        }
        catch (Exception ex)
        {
            ReportException(ex, fatal: true);
        }
    }

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        // UI 线程异常不直接退出，避免窗口闪退；同时写入本地日志便于排查。
        ReportException(e.Exception, fatal: false);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            TryWriteLog(exception);
        }
    }

    private static void ReportException(Exception exception, bool fatal)
    {
        if (Interlocked.Exchange(ref _reportingException, 1) != 0)
        {
            return;
        }

        try
        {
            TryWriteLog(exception);

            string prefix = fatal
                ? "程序遇到无法恢复的错误，即将退出。"
                : "游戏遇到异常，已尝试继续运行。";
            MessageBox.Show(
                $"{prefix}\n\n{exception.Message}",
                "Tetris",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch
        {
            // 记录日志或弹窗失败时不再抛出第二个异常。
        }
        finally
        {
            Volatile.Write(ref _reportingException, 0);
        }
    }

    private static void TryWriteLog(Exception exception)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Tetris");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}");
        }
        catch
        {
            // 日志目录不可写时静默忽略，不能影响主流程。
        }
    }
}
