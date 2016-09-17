public static class LogHelper
{
    private static TraceWriter logger;

    public static void Initialize(TraceWriter log)
    {
        if (logger == null) logger = log;
    } 

    public static void Info(string logtext)
    {
        logger.Info(logtext); 
    }

    public static void Error(string logtext)
    {
        logger.Error(logtext); 
    }
}