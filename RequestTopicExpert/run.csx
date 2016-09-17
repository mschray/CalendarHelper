//https://azure.microsoft.com/en-us/documentation/articles/functions-reference-csharp/
#load "..\shared\DocumentDBHelper.csx"
#load "..\shared\LogHelper.csx"
#load "..\shared\AppSettingsHelper.csx"
using System;
using System.Net;
using System.Net.Mail;
using System.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

private static string BccEmailAddress = "";
private static string SchedulerEmailAddress = "";
private static string FromEmailAddress = "";
private static Dictionary<string,string> ExpertDictionary;

public static async Task<Document> LogRequest(ExpertRequest Request)
{

    LogHelper.Info($"Log Request input ={Request}."); 

    Document doc=null;

    try
    {
        string DocDBEndpoint = ConfigurationManager.AppSettings["DOCDB_ENDPOINT"].ToString();

        string DocDBAuthKey = ConfigurationManager.AppSettings["DOCDB_AUTHKEY"].ToString();

        string ExpertRequestDBName = ConfigurationManager.AppSettings["EXPERT_REQUEST_DBNAME"].ToString();
        string ExperRequestColName = ConfigurationManager.AppSettings["EXPERT_REQUEST_COLLNAME"].ToString();

        using (DocumentClient client = new DocumentClient(new Uri(DocDBEndpoint), DocDBAuthKey))
        {
            Database db = await  DocDBHelper.GetOrCreateDatabaseAsync(client, ExpertRequestDBName );
            DocumentCollection col = await  DocDBHelper.GetOrCreateCollectionAsync(client, ExpertRequestDBName,  ExperRequestColName);
            doc = await client.CreateDocumentAsync(col.SelfLink, Request );
        }
    }
    catch (Exception ex)
    {
        LogHelper.Info($"Input ={Request} had an exception of {ex.Message}."); 
    }

    return doc;
}

static void LoadEmailConfiguration()
{
    BccEmailAddress = ConfigurationManager.AppSettings["BCC_EMAIL"].ToString();
    SchedulerEmailAddress = ConfigurationManager.AppSettings["SCHEDULER_EMAIL"].ToString();
    FromEmailAddress = ConfigurationManager.AppSettings["FROM_EMAL"].ToString();
    LogHelper.Info($"BCC EMAIL ={BccEmailAddress}.");
    LogHelper.Info($"SCHEDULER EMAIL ={SchedulerEmailAddress}.");
    LogHelper.Info($"FROM EMAIL ={FromEmailAddress}.");

}

static void LoadTopicExperts()
{
    ExpertDictionary = new Dictionary<string, string>();
    
    // grab from app setting, delimited by ; for topics and experts
    //"Node","foo@microsoft.com";"Azure Functions","foo1@microsoft.com";"Azure App Services","foo2@microsoft.com"
    string Experts = ConfigurationManager.AppSettings["EXPERTS_LIST"].ToString();

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

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    // Grab the log and make it a class variable that other methods can use
    LogHelper.Initialize(log);

    // get email address to use from App settings
    LoadEmailConfiguration();

    LogHelper.Info($"C# HTTP trigger function processed a request. Request Content={await req.Content.ReadAsStringAsync()}");
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
                // setup the other to addresss
                List<Personalization> Personalization = new List<Personalization>();    
                //Personalization.Add(new Personalization());
                
                // set up another recipent - calendar help
                Personalization personalization = new Personalization();
                personalization.AddTo(new Email(SchedulerEmailAddress));  
                personalization.AddTo(new Email(aExpertRequest.ReqestorEmailAddress));  
                personalization.AddTo(new Email(GetExpert(aExpertRequest.Topic,aExpertRequest.RequestedConversation)));

                string SendGridKey = ConfigurationManager.AppSettings["SEND_GRID_API_KEY"].ToString();
                //LogHelper.Info($"The retrived key is {SendGridKey}");
                //Console.WriteLine($"The retrived key is {SendGridKey}");
                
                dynamic sg = new SendGridAPIClient(SendGridKey);
    
                Email from = new Email(FromEmailAddress);

                string subject = "Expert Scheduling request";
    
                Email to = new Email(GetExpert(aExpertRequest.Topic,aExpertRequest.RequestedConversation));
        
                string messageContent = $"Edi, can you scheudle a Skype for business call between the people on the to line tomorrow {aExpertRequest.RequestedDayHalf.ToString().ToLower()}?" 
                    +"\n" + $"The conversation will cover the following, {aExpertRequest.RequestedConversation}";
                Content content = new Content("text/plain", messageContent);
    
                Mail mail = new Mail(from, subject, to, content);
                mail.AddPersonalization(personalization);
                mail.MailSettings = GetMailSettings(aExpertRequest.IsTest);
                
                LogHelper.Info($"Email body\n {mail.Get()} ");
    
                dynamic response = await sg.client.mail.send.post(requestBody: mail.Get());

                await LogRequest(aExpertRequest);
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