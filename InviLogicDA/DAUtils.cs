using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Forge;
using Autodesk.Forge.DesignAutomation.v3;
using Autodesk.Forge.Model;
using Autodesk.Forge.Model.DesignAutomation.v3;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using RestSharp;
using ActivitiesApi = Autodesk.Forge.DesignAutomation.v3.ActivitiesApi;
using Activity = Autodesk.Forge.Model.DesignAutomation.v3.Activity;
using WorkItem = Autodesk.Forge.Model.DesignAutomation.v3.WorkItem;
using WorkItemsApi = Autodesk.Forge.DesignAutomation.v3.WorkItemsApi;
using System.Text.RegularExpressions;

namespace InviLogicDA
{
    class DAUtils
    {
        static string ConsumerKey =  Environment.GetEnvironmentVariable("FORGE_CLIENT_ID");
        static string ConsumerSecret = Environment.GetEnvironmentVariable("FORGE_CLIENT_SECRET");
        static string EngineName = "Autodesk.Inventor+23";
        static string LocalAppPackageZip = Path.GetFullPath(@"..\..\..\..\UpdateParams\UpdateParamsBundle\UpdateParamsBundle.zip");
        static string APPNAME = "iLogicDA";
        static string ACTIVITY_NAME = "iLogicDActivity";
        static string ALIAS = "v1";
        static string inputFile =Path.GetFullPath(@"..\..\..\InputPart\Rim.ipt"); 
        static string inputFileName = Path.GetFileName(inputFile);
        static string outputFileName = "Result.ipt";
        static string uploadURL = string.Empty;
        public static int paramColor { get; set; } // 1 - Chrome ; 2 - Brass ; 3 - Copper
        public static int paramRim { get; set; } // 1 - 5 spoke; 2 - Multi spoke

        public static dynamic InternalToken { get; set; }
   
        public class Output
        {
            public StatusEnum Status { get; set; }
            public string Message { get; set; }
            public Output(StatusEnum status, string message)
            {
                Status = status;
                Message = message;
                Console.WriteLine(status + ":" + message);
            }
            public enum StatusEnum
            {
                Error,
                Sucess
            }
        }
        /// <summary>
        /// Creates WorkItem
        /// </summary>
        /// <returns>True if successful</returns>
        public static async Task<dynamic> CreateWorkItem(String bucketkey)
        {
            string nickName = ConsumerKey;
            Bearer bearer = (await Get2LeggedTokenAsync(new Scope[] { Scope.CodeAll })).ToObject<Bearer>();
            string downloadUrl = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketkey, inputFileName);
            string uploadUrl = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketkey, outputFileName);
            JObject iptFile = new JObject
                {
                  new JProperty("url", downloadUrl),
                  new JProperty("headers",
                  new JObject{
                    new JProperty("Authorization", "Bearer " + InternalToken.access_token)
                  })
                };

            JObject inputParams = new JObject
            {
                new JProperty("url","data:application/json,{\"Color\":\"" + paramColor.ToString() + "\", \"RimStyle\":\"" + paramRim.ToString() +"\"}")
            };

            JObject resultIpt = new JObject
                {
                  new JProperty("verb", "put"),
                  new JProperty("url", uploadUrl),
                  new JProperty("headers",
                  new JObject{
                    new JProperty("Authorization", "Bearer " + InternalToken.access_token)
                  })
                };
            WorkItem workItemSpec = new WorkItem(
              null, string.Format("{0}.{1}+{2}", nickName, ACTIVITY_NAME, ALIAS),
              new Dictionary<string, JObject>()
              {{ "InputIPT",  iptFile },{"InputParams", inputParams } ,{ "ResultIPT", resultIpt  }}, null);
            WorkItemsApi workItemApi = new WorkItemsApi();
            workItemApi.Configuration.AccessToken = bearer.AccessToken;
            WorkItemStatus newWorkItem = await workItemApi.WorkItemsCreateWorkItemsAsync(null, null, workItemSpec);

            for (int i = 0; i < 1000; i++)
            {
                System.Threading.Thread.Sleep(1000);
                WorkItemStatus workItemStatus = await workItemApi.WorkItemsGetWorkitemsStatusAsync(newWorkItem.Id);

                if (workItemStatus.Status == WorkItemStatus.StatusEnum.Pending || workItemStatus.Status == WorkItemStatus.StatusEnum.Inprogress) continue;
                break;
            }
            uploadURL = uploadUrl;
            ObjectsApi objectsapi = new ObjectsApi();
            objectsapi.Configuration.AccessToken = InternalToken.access_token;
            dynamic obj = await objectsapi.GetObjectDetailsAsync(bucketkey, outputFileName);
            return obj;
        }


        /// <summary>
        /// Creates Activity
        /// </summary>
        /// <returns>True if successful</returns>
        public static async Task<dynamic> CreateActivity()
        {
            Bearer bearer = (await Get2LeggedTokenAsync(new Scope[] { Scope.CodeAll })).ToObject<Bearer>();
            string nickName = ConsumerKey;
      
            AppBundlesApi appBundlesApi = new AppBundlesApi();
            appBundlesApi.Configuration.AccessToken = bearer.AccessToken;
            PageString appBundles = await appBundlesApi.AppBundlesGetItemsAsync();
            string appBundleID = string.Format("{0}.{1}+{2}", nickName, APPNAME, ALIAS);

            if (!appBundles.Data.Contains(appBundleID))
            {
                if (!System.IO.File.Exists(LocalAppPackageZip)) return new Output(Output.StatusEnum.Error, "Bundle not found at " + LocalAppPackageZip);
                // create new bundle
                AppBundle appBundleSpec = new AppBundle(APPNAME, null, EngineName, null, null, APPNAME, null, APPNAME);
                AppBundle newApp = await appBundlesApi.AppBundlesCreateItemAsync(appBundleSpec);
                if (newApp == null) return new Output(Output.StatusEnum.Error, "Cannot create new app");
                // create alias
                Alias aliasSpec = new Alias(1, null, ALIAS);
                Alias newAlias = await appBundlesApi.AppBundlesCreateAliasAsync(APPNAME, aliasSpec);
                // upload the zip bundle
                RestClient uploadClient = new RestClient(newApp.UploadParameters.EndpointURL);
                RestRequest request = new RestRequest(string.Empty, Method.POST);
                request.AlwaysMultipartFormData = true;
                foreach (KeyValuePair<string, object> x in newApp.UploadParameters.FormData)
                    request.AddParameter(x.Key, x.Value);
                request.AddFile("file", LocalAppPackageZip);
                request.AddHeader("Cache-Control", "no-cache");
                var res = await uploadClient.ExecuteTaskAsync(request);
            }
            ActivitiesApi activitiesApi = new ActivitiesApi();
            activitiesApi.Configuration.AccessToken = bearer.AccessToken;
            PageString activities = await activitiesApi.ActivitiesGetItemsAsync();

            string activityID = string.Format("{0}.{1}+{2}", nickName, ACTIVITY_NAME, ALIAS);
            if (!activities.Data.Contains(activityID))
            {
                // create activity
                string commandLine = string.Format(@"$(engine.path)\\inventorcoreconsole.exe /i $(args[InputIPT].path) /al $(appbundles[{0}].path) $(args[InputParams].path)", APPNAME);
                ModelParameter iptFile = new ModelParameter(false, false, ModelParameter.VerbEnum.Get, "Input Ipt File", true, inputFileName);
                ModelParameter result = new ModelParameter(false, false, ModelParameter.VerbEnum.Put, "Resulting Ipt File", true, outputFileName);
                ModelParameter inputParams = new ModelParameter(false, false, ModelParameter.VerbEnum.Get, "Input params", false, "params.json");
                Activity activitySpec = new Activity(
                 new List<string> { commandLine },
                  new Dictionary<string, ModelParameter>()
                  {
                         { "InputIPT", iptFile },
                         { "InputParams", inputParams},
                         { "ResultIPT",result},
                  },
                  EngineName,
                  new List<string>() { string.Format("{0}.{1}+{2}", nickName, APPNAME, ALIAS) },
                  null,
                  ACTIVITY_NAME,
                  null,
                  ACTIVITY_NAME
                );
                Activity newActivity = await activitiesApi.ActivitiesCreateItemAsync(activitySpec);
                Alias aliasSpec = new Alias(1, null, ALIAS);
                Alias newAlias = await activitiesApi.ActivitiesCreateAliasAsync(ACTIVITY_NAME, aliasSpec);
            }
            return new Output(Output.StatusEnum.Sucess, "Activity created");
        }

        /// <summary>
        /// Fetches the internal token
        /// </summary>
        /// <returns>Internal token</returns>
        public async static Task<dynamic> GetInternalAsync()
        {
            if (InternalToken == null || InternalToken.ExpiresAt < DateTime.UtcNow)
            {
                InternalToken = await Get2LeggedTokenAsync(new Scope[] { Scope.BucketCreate, Scope.BucketRead, Scope.DataRead, Scope.DataWrite });
                InternalToken.ExpiresAt = DateTime.UtcNow.AddSeconds(InternalToken.expires_in);
            }
            return InternalToken;
        }

        /// <summary>
        /// Fetches the token based on the scope argument
        /// </summary>
        /// <returns>token</returns>
        private async static Task<dynamic> Get2LeggedTokenAsync(Scope[] scopes)
        {
            TwoLeggedApi oauth = new TwoLeggedApi();
            string grantType = "client_credentials";
            dynamic bearer = await oauth.AuthenticateAsync(
             ConsumerKey,
             ConsumerSecret,
              grantType,
              scopes);
            return bearer;
        }

        /// <summary>
        /// Creates Bucket
        /// </summary>
        /// <returns>Newly created bucket</returns>
        public async static Task<dynamic> CreateBucket()
        {
            string bucketKey = "inventorilogicda" + ConsumerKey.ToLower();
            BucketsApi bucketsApi = new BucketsApi();
            bucketsApi.Configuration.AccessToken = InternalToken.access_token;
            dynamic buckets = await bucketsApi.GetBucketsAsync();
            bool bucketExists = buckets.items.ToString().Contains(bucketKey);
            if (!bucketExists)
            {
                PostBucketsPayload postBucket = new PostBucketsPayload(bucketKey, null, PostBucketsPayload.PolicyKeyEnum.Transient);
                dynamic newbucket = await bucketsApi.CreateBucketAsync(postBucket);
            }
            return bucketKey;
        }

        /// <summary>
        /// Uploads Ipt file from local to bucket
        /// </summary>
        /// <returns>uploaded file</returns>
        public async static Task<dynamic> UploadIptFile(string bucketKey)
        {
            ObjectsApi objects = new ObjectsApi();
            objects.Configuration.AccessToken = InternalToken.access_token;
            dynamic uploadedObj = null;

            using (StreamReader streamReader = new StreamReader(inputFile))
            {
                uploadedObj = await objects.UploadObjectAsync(bucketKey,
                      inputFileName, (int)streamReader.BaseStream.Length, streamReader.BaseStream,
                      "application/octet-stream");
            }
            return uploadedObj;
        }

        /// <summary>
        /// Translate the uploaded file.
        /// </summary>
        /// <returns>translated object</returns>
        public async static Task<dynamic> TranslateIptFile(dynamic newObject)
        {
            string objectIdBase64 = ToBase64(newObject.objectId);
            List<JobPayloadItem> postTranslationOutput = new List<JobPayloadItem>()
                {
                    new JobPayloadItem(
                    JobPayloadItem.TypeEnum.Svf,
                    new List<JobPayloadItem.ViewsEnum>()
                    {
                        JobPayloadItem.ViewsEnum._3d,
                        JobPayloadItem.ViewsEnum._2d
                    })
                };

            JobPayload postTranslation = new JobPayload(
                new JobPayloadInput(objectIdBase64, false, outputFileName),
                new JobPayloadOutput(postTranslationOutput));
            DerivativesApi derivativeApi = new DerivativesApi();
            derivativeApi.Configuration.AccessToken = InternalToken.access_token;
            dynamic translation = await derivativeApi.TranslateAsync(postTranslation,true);

            // check if it is complete.
            int progress = 0;
            do
            {
                System.Threading.Thread.Sleep(1000); // wait 1 second
                try
                {
                    dynamic manifest = await derivativeApi.GetManifestAsync(objectIdBase64);
                    progress = (string.IsNullOrWhiteSpace(Regex.Match(manifest.progress, @"\d+").Value) ? 100 : Int32.Parse(Regex.Match(manifest.progress, @"\d+").Value));
                }
                catch (Exception) { }
            } while (progress < 100);
            return translation;
        }

        /// <summary>
        /// Downloads the Result.ipt file to the current user's documents folder 
        /// </summary> 
        /// <returns>True if successful</returns>
        public static async Task<dynamic> DownloadToDocs()
        {
            if (uploadURL == string.Empty)
            { return new Output(Output.StatusEnum.Error, "Upload URL is empty");  }

            string OutputiptFile = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Result.ipt";
            IRestClient client = new RestClient("https://developer.api.autodesk.com/");
            RestRequest request = new RestRequest(uploadURL, Method.GET);
            request.AddHeader("Authorization", "Bearer " + InternalToken.access_token);
            request.AddHeader("Accept-Encoding", "gzip, deflate");
            IRestResponse response = await client.ExecuteTaskAsync(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return new Output(Output.StatusEnum.Error, "Not able to download to local drive");
            }
            else
            {
                File.WriteAllBytes(OutputiptFile, response.RawBytes);
                return new Output(Output.StatusEnum.Sucess, "Downloaded successfully");
            }
        }
        /// <summary>
        /// Convert a string into Base64 (source http://stackoverflow.com/a/11743162).
        /// </summary>  
        /// <returns>base64 string</returns>
        private static string ToBase64(string input)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(input);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}

