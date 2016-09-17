#load "..\shared\LogHelper.csx"
#load "..\shared\AppSettingsHelper.csx"
using System.Net;

private static TraceWriter logger;

/// Extract the topics from the and topics and experts list so there is one master list
/// and topics and experts stay in sync.
private static string[] LoadTopics()
{
    
    string TopicsAndExperts = AppSettingsHelper.GetAppSetting("EXPERTS_LIST");
     
    LogHelper.Info($"Got TopicsAndExperts: {TopicsAndExperts}");

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
        LogHelper.Info($"Loaded topic: {TopicDetails[0]}");
    }

    return Topics.ToArray();

}

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    // save the logging context for later use
    LogHelper.Initialize(log);

    LogHelper.Info($"C# HTTP trigger function processed a request. RequestUri={req.RequestUri}");

    string[] Topics = LoadTopics();

    return Topics == null
        ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a name on the query string or in the request body")
        : req.CreateResponse(HttpStatusCode.OK, Topics);
}