using System.Net;
using System.Configuration;


private static TraceWriter logger;

private static string[] LoadTopics()
{
    string TopicsAndExperts = ConfigurationManager.AppSettings["EXPERTS_LIST"].ToString();
     
    logger.Info($"Got TopicsAndExperts: {TopicsAndExperts}");

    // split each topic and expert pair
    string[] ExpertList = TopicsAndExperts.Split(';');

    // Create container to return
    List<string> Topics = new List<string>();

    foreach (var item in ExpertList)
    {
        // split topic from expert
        string[] TopicDetails = item.Split(',');

        // load topic
        Topics.Add(TopicDetails[0]);
        logger.Info($"Loaded topic: {TopicDetails[0]}");
    }

    return Topics.ToArray();

}

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    // save the logging context for later use
    logger = log;

    log.Info($"C# HTTP trigger function processed a request. RequestUri={req.RequestUri}");

    string[] Topics = LoadTopics();

    return Topics == null
        ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a name on the query string or in the request body")
        : req.CreateResponse(HttpStatusCode.OK, Topics);
}