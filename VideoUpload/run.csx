#r "Microsoft.WindowsAzure.Storage"

using Microsoft.WindowsAzure.Storage.Blob;

using System.Net;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, Binder binder, TraceWriter log)
{
  var bytes = req.Content.ReadAsByteArrayAsync().Result;

  string sourceName = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "SourceName", true) == 0)
        .Value;

   log.Info(bytes.Length.ToString());   

   var now = DateTime.UtcNow.AddHours(11);

   var today = now.ToString("yyyy-MM-dd");

   var gName = $"video/{sourceName}/{today}/{now.Ticks.ToString()}.mp4";

    var attributes = new Attribute[]
    {
        new BlobAttribute(gName),
        new StorageAccountAttribute("jortana_STORAGE")
    };


    var writer = await binder.BindAsync<CloudBlockBlob>(attributes);
    
    await writer.UploadFromByteArrayAsync(bytes, 0, bytes.Length);
    
  
   return req.CreateResponse(HttpStatusCode.OK, "Hello ");
}