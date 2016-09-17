#load "LogHelper.csx"
#load "DocumentDBHelper.csx"
#load "AppSettingsHelper.csx"
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

public static class DocDBLogger
{
    public static async Task<Document> LogRequest(ExpertRequest Request)
    {

        LogHelper.Info($"Log Request input ={Request}."); 

        Document doc=null;

        try
        {
            string DocDBEndpoint = AppSettingsHelper.GetAppSetting("DOCDB_ENDPOINT");

            string DocDBAuthKey = AppSettingsHelper.GetAppSetting("DOCDB_AUTHKEY",false);

            string ExpertRequestDBName = AppSettingsHelper.GetAppSetting("EXPERT_REQUEST_DBNAME");
            string ExperRequestColName = AppSettingsHelper.GetAppSetting("EXPERT_REQUEST_COLLNAME");

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
}