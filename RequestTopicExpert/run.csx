//https://azure.microsoft.com/en-us/documentation/articles/functions-reference-csharp/
#load "..\shared\DocDBLogger.csx"
#load "..\shared\LogHelper.csx"
#load "..\shared\AppSettingsHelper.csx"
using System;
using System.Net;
using System.Net.Mail;
using SendGrid;
using SendGrid.Helpers.Mail;
using Newtonsoft.Json;

private static string BccEmailAddress = "";
private static string SchedulerEmailAddress = "";
private static string FromEmailAddress = "";
private static Dictionary<string,string> ExpertDictionary;

static void LoadEmailConfiguration()
{
    BccEmailAddress = AppSettingsHelper.GetAppSetting("BCC_EMAIL");
    SchedulerEmailAddress = AppSettingsHelper.GetAppSetting("SCHEDULER_EMAIL");
    FromEmailAddress = AppSettingsHelper.GetAppSetting("FROM_EMAL");
}

static void LoadTopicExperts()
{
    ExpertDictionary = new Dictionary<string, string>();
    
    // grab from app setting, delimited by ; for topics and experts
    //"Node","foo@microsoft.com";"Azure Functions","foo1@microsoft.com";"Azure App Services","foo2@microsoft.com"
    string Experts = AppSettingsHelper.GetAppSetting("EXPERTS_LIST");

    LogHelper.Info($"EXPERTS ={Experts}.");

    string[] ExpertList = Experts.Split(';');

    foreach (var item in ExpertList)
    {
        string[] TopicDetails = item.Split(',');
        ExpertDictionary.Add(TopicDetails[0],TopicDetails[1]);
        LogHelper.Info($"Loaded topic: {TopicDetails[0]} with expert of {TopicDetails[1]}");
    }
}

public static string GetExpert(string Topic, string Conversation)
{
    if (ExpertDictionary==null) LoadTopicExperts();
    
    string name = ExpertDictionary
        .FirstOrDefault(q => string.Compare(q.Key, Topic, true) == 0)
        .Value;

    if (name == null)
        LogHelper.Info($"For {Topic} I didn't find an expert."); 
    else
        LogHelper.Info($"For {Topic} I found an expert in {name}.");

    return name == null ? "mschray@microsoft.com" : name;    
}

public static MailSettings GetMailSettings(bool SandBox)
{
    MailSettings mailSettings = new MailSettings();

    BCCSettings bccSettings = new BCCSettings();
    
    bccSettings.Enable = true;

    bccSettings.Email = BccEmailAddress;

    mailSettings.BccSettings = bccSettings;
    
    if (SandBox)
    {
        SandboxMode sandboxMode = new SandboxMode();

        sandboxMode.Enable = true;

        mailSettings.SandboxMode = sandboxMode;
        
    }

    return mailSettings;
}

public static async Task<int> SendMail(ExpertRequest aExpertRequest)
{

    string SendGridKey = AppSettingsHelper.GetAppSetting("SEND_GRID_API_KEY",false);
    
    dynamic sg = new SendGridAPIClient(SendGridKey);

    Email from = new Email(FromEmailAddress);

    string subject = "Expert Scheduling request";

    Email to = new Email(GetExpert(aExpertRequest.Topic,aExpertRequest.RequestedConversation));

    string messageContent = $"Edi, can you scheudle a Skype for business call between the people on the to line tomorrow {aExpertRequest.RequestedDayHalf.ToString().ToLower()}?" 
        +"\n" + $"The conversation will cover the following, {aExpertRequest.RequestedConversation}";
    Content content = new Content("text/plain", messageContent);

    Mail mail = new Mail(from, subject, to, content);

    // set up another recipents - calendar help, the expert and the customer
    // because the to line will only take ONE address 
    Personalization Personalization = new Personalization();
    Personalization.AddTo(new Email(SchedulerEmailAddress));  
    Personalization.AddTo(new Email(aExpertRequest.ReqestorEmailAddress));  
    Personalization.AddTo(new Email(GetExpert(aExpertRequest.Topic,aExpertRequest.RequestedConversation)));

    mail.AddPersonalization(Personalization);
    mail.MailSettings = GetMailSettings(aExpertRequest.IsTest);
    
    LogHelper.Info($"Email body\n {mail.Get()} ");

    dynamic response = await sg.client.mail.send.post(requestBody: mail.Get());

    // got to permanent record
    await DocDBLogger.LogRequest(aExpertRequest);

    return 0;
}

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    // Grab the log and make it a class variable that other methods can use
    LogHelper.Initialize(log);

    // get email address to use from App settings
    LoadEmailConfiguration();

    LogHelper.Info($"C# HTTP trigger function processed a request. Request Content={await req.Content.ReadAsStringAsync()}");

    // parse query parameter
    string name = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "Topic", true) == 0)
        .Value;

    // get request body
    dynamic data = await req.Content.ReadAsAsync<object>();
    ExpertRequest aExpertRequest = new ExpertRequest();
    aExpertRequest.Topic = data?.Topic;
    aExpertRequest.ReqestorFirstName = data?.ReqestorFirstName;
    aExpertRequest.ReqestorLastName = data?.ReqestorLastName;
    aExpertRequest.ReqestorEmailAddress = data?.ReqestorEmailAddress;
    aExpertRequest.RequestedConversation = data?.RequestedConversation;
    aExpertRequest.RequestedDayHalf = data?.RequestedDayHalf;
    aExpertRequest.IsTest = data?.IsTest;
    
    LogHelper.Info($"Dyanmic data is Request Content={data}");

    LogHelper.Info($"conversation={aExpertRequest.RequestedConversation}");

    if (aExpertRequest.ReqestorEmailAddress == null)
    {
        return req.CreateResponse(HttpStatusCode.BadRequest, "Please pass all the fields in the request body");
    }
    else
    {

        try
        {

            await SendMail(aExpertRequest);
        }
        catch (Exception ex)
        {
            LogHelper.Error($"RequestTopicExpert function had an error.  The message was {ex.Message} an inner excetion of {ex.InnerException} and a stacktrace of {ex.StackTrace}. ");
        }
            
    }

    return aExpertRequest.ReqestorEmailAddress == null
        ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass in all the fields in the query string or in the request body")
        : req.CreateResponse(HttpStatusCode.OK, "Making expert request");
}


public enum DayHalf {Morning, Afternoon};

public class ExpertRequest
{
    public string Topic {get;set;}
    public string ReqestorFirstName {get;set;}
    public string ReqestorLastName {get;set;}
    public string ReqestorEmailAddress {get;set;}
    public string RequestedConversation {get; set;}
    public DayHalf RequestedDayHalf {get; set;}
    public bool   IsTest {get; set;}
}