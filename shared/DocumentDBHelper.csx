using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

public static class DocDBHelper
{



    public static async Task<Database> GetOrCreateDatabaseAsync(DocumentClient client, string id)
    {
        Database database = client.CreateDatabaseQuery().Where(db => db.Id == id).ToArray().SingleOrDefault();

        if (database == null)
        {
            database = await client.CreateDatabaseAsync(new Database { Id = id });
        }

        return database;
    }

    public static async Task<DocumentCollection> GetOrCreateCollectionAsync(DocumentClient client, string databaseId, string collectionId)
    {

        DocumentCollection collection = client.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(databaseId))
            .Where(c => c.Id == collectionId).ToArray().SingleOrDefault();

        if (collection == null)
        {
            collection = await CreateDocumentCollectionWithRetriesAsync(client, databaseId, new DocumentCollection { Id = collectionId });
        }
        return collection;
    }

    public static async Task<DocumentCollection> CreateDocumentCollectionWithRetriesAsync(DocumentClient client, string databaseId, DocumentCollection collectionDefinition, 
        int? offerThroughput = 400)
    {

        return await ExecuteWithRetries(

            client,

            () => client.CreateDocumentCollectionAsync(

                    UriFactory.CreateDatabaseUri(databaseId),

                    collectionDefinition,

                    new RequestOptions { OfferThroughput = offerThroughput }));

    }


    public static async Task<V> ExecuteWithRetries<V>(DocumentClient client, Func<Task<V>> function)

    {

        TimeSpan sleepTime = TimeSpan.Zero;



        while (true)

        {

            try

            {

                return await function();

            }

            catch (DocumentClientException de)

            {

                if ((int)de.StatusCode != 429 && (int)de.StatusCode != 449)

                {

                    throw;

                }



                sleepTime = de.RetryAfter;

            }

            catch (AggregateException ae)

            {

                if (!(ae.InnerException is DocumentClientException))

                {

                    throw;

                }



                DocumentClientException de = (DocumentClientException)ae.InnerException;

                if ((int)de.StatusCode != 429)

                {

                    throw;

                }



                sleepTime = de.RetryAfter;

                if (sleepTime < TimeSpan.FromMilliseconds(10))

                {

                    sleepTime = TimeSpan.FromMilliseconds(10);

                }

            }



            await Task.Delay(sleepTime);

        }

    }
}