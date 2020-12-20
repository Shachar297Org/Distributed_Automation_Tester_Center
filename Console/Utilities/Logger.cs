using Serilog;

namespace Console.Utilities
{
    public class Logger
    {
        public Logger(string logfile)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logfile)
                .CreateLogger();
        }

        public void WriteLog(string msg, string level)
        {
            if (level == "info")
            {
                Log.Information(msg);
            }
            if (level == "warning")
            {
                Log.Warning(msg);
            }
            if (level == "error")
            {
                Log.Error(msg);
            }
        }
    }
}
