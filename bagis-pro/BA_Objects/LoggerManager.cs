using NLog;

namespace bagis_pro.BA_Objects
{
    public interface ILoggerManager
    {
        void LogInfo(string message);
        void LogWarn(string message);
        void LogDebug(string message);
        void LogError(string message);
        void UpdateLogFileLocation(string strFilePath);
    }

    public class LoggerManager : ILoggerManager
    {
        private ILogger logger;
        //https://stackoverflow.com/questions/3000653/using-nlog-as-a-rollover-file-logger
        //configuration information
        public LoggerManager(string nLogConfigLocation)
        {
            LogManager.LoadConfiguration(nLogConfigLocation);
            this.logger = LogManager.GetCurrentClassLogger();
        }

        public void LogDebug(string message)
        {
            this.logger.Debug(message);
        }

        public void LogError(string message)
        {
            this.logger.Error(message);
        }

        public void LogInfo(string message)
        {
            this.logger.Info(message);
        }

        public void LogWarn(string message)
        {
            this.logger.Warn(message);
        }

        public void UpdateLogFileLocation (string strFilePath)
        {
            GlobalDiagnosticsContext.Set("AoiLogFolder", strFilePath);
        }
    }
}
