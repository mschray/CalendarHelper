//https://azure.microsoft.com/en-us/documentation/articles/functions-reference-csharp/
#load "DocumentDBHelper.csx"
using System;
using System.Net;
using System.Net.Mail;
using System.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

private static string BccEmailAddress = "mschray@microsoft";
private static string SchedulerEmailAddress = "edi@calendar.help";
//private static string SchedulerEmailAddress = "martin.schray@hotmail.com";
private static string FromEmailAddress = "mschray@microsoft.com";
private static Dictionary<string,string> ExpertDictionary;
private static TraceWriter logger;

public static async Task<Document> LogRequest(ExpertRequest Request)
{

     logger.Info($"Log Request input ={Request}."); 

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
        logger.Info($"Input ={Request} had an exception of {ex.Message}."); 
    }

    return doc;
}


static void LoadTopicExperts()
{
    ExpertDictionary = new Dictionary<string, string>();
    
    ExpertDictionary.Add("Node","ryanjoy@microsoft.com");
    ExpertDictionary.Add("Azure Functions","mschray@microsoft.com");
    ExpertDictionary.Add("Azure App Services","mschray@microsoft.com");

    foreach (var item in collection)
    {
        logger.Info($"Loaded topic: {item.ToString()}");
        
    }
}

public static string GetExpert(string Topic, string Conversation)
{
    if (ExpertDictionary==null) LoadTopicExperts();
    
    string name = ExpertDictionary
        .FirstOrDefault(q => string.Compare(q.Key, Topic, true) == 0)
        .Value;

    if (name == null)
        logger.Info($"For {Topic} I didn't find an expert."); 
    else
        logger.Info($"For {Topic} I found an expert in {name}.");

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
    logger = log;
    
    log.Info($"C# HTTP trigger function processed a request. Request Content={await req.Content.ReadAsStringAsync()}");

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
    
    log.Info($"Dyanmic data is Request Content={data}");

    log.Info($"conversation={aExpertRequest.RequestedConversation}");

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
                //log.Info($"The retrived key is {SendGridKey}");
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
                
                log.Info($"Email body\n {mail.Get()} ");
    
                dynamic response = await sg.client.mail.send.post(requestBody: mail.Get());

                await LogRequest(aExpertRequest);
            }
            catch (Exception ex)
            {
                log.Info($"RequestTopicExpert function had an error.  The message was {ex.Message} an inner excetion of {ex.InnerException} and a stacktrace of {ex.StackTrace}. ");
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