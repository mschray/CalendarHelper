public static class LogHelper
{
    private static TraceWriter logger;

    public static void InitializeLogger(TraceWriter log)
    {
        if (!logger) logger = log;
    } 

    public static void Info(TraceWriter log, string logtext)
    {
        logger.Info(logtext); 
    }

    public static void Error(TraceWriter log, string logtext)
    {
        logger.Error(logtext); 
    }
}