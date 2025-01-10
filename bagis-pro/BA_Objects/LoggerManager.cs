using NLog;

namespace bagis_pro.BA_Objects
{
    public interface ILoggerManager
    {
        void LogInfo(string functionName, string message);
        void LogWarn(string functionName, string message);
        void LogDebug(string functionName, string message);
        void LogError(string functionName, string message);
        void UpdateLogFileLocation(string strFilePath);
    }

    public class LoggerManager : ILoggerManager
    {
        private ILogger logger;
        //https://stackoverflow.com/questions/3000653/using-nlog-as-a-rollover-file-logger
        //configuration information
        public LoggerManager(string nLogConfigLocation)
        {
            //LogManager.LoadConfiguration(nLogConfigLocation);
            LogManager.Setup().LoadConfigurationFromFile(nLogConfigLocation);
            this.logger = LogManager.GetCurrentClassLogger();
        }

        public void LogDebug(string functionName, string message)
        {
            this.logger.Debug(functionName + ": " + message);
        }

        public void LogError(string functionName, string message)
        {
            this.logger.Error(functionName + ": " + message);
        }

        public void LogInfo(string functionName, string message)
        {
            this.logger.Info(functionName + ": " + message);
        }

        public void LogWarn(string functionName, string message)
        {
            this.logger.Warn(functionName + ": " + message);
        }

        public void UpdateLogFileLocation (string strFilePath)
        {
            GlobalDiagnosticsContext.Set("AoiLogFolder", strFilePath);
        }
    }
}
