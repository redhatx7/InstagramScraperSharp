using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace InstagramScraperSharp
{
   public class InstagramWebClient
    {
        private const string BASE_URL = "https://instagram.com/";
        private const string GET_ID_URL = "web/search/topsearch/?context=blended&query=";
        private const string GRAPHQL = "graphql/query/?query_hash={0}&variables={1}";
        private const string USER_AGENT =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36";

        private string MediaUrlFormat = "p/{0}/?__a=1";
        private string Format = "{id:\"{0}\",first:{1},after:\"{2}\"";
        private string CsrfToken { set; get; }
        private string HashQuery { set; get; }
        private string EndCursor  {set;get;}
        private string Username { set; get; }
        private string UserId { set; get; }
        private string GISHash { set; get; }

        public InstagramWebClient(string username)
        {
            this.Username = username;
            InitialRequest().Wait();
            this.UserId = GetUserId().Result;
        }
        public struct Response
        {
            public string ResponseBody;
            public WebHeaderCollection Headers;
            public CookieContainer Cookies;

            public Response(string responseBody,WebHeaderCollection headers,CookieContainer cookies)
            {
                ResponseBody = responseBody;
                Headers = headers;
                Cookies = cookies;
            }
        }
        public struct HashData
        {
            public string HashId;
            public string EndCursor;

            public HashData(string hashId,string endCursor)
            {
                HashId = hashId;
                EndCursor = endCursor;
            }
        }
        
        
        public struct Media
        {
            public string DisplayUrl;
            public string Text;
            public List<string> EdgeSidecar;

            public Media(string displayUrl, string text, List<string> edgeSidecar)
            {
                DisplayUrl = displayUrl;
                Text = text;
                EdgeSidecar = edgeSidecar;
            }
            
        }

        private string GetData(string userId, int first , string after)
        {
            return "{" + $"\"id\":\"{userId}\",\"first\":{first},\"after\":\"{after}\"" + "}";
        }
        private string GetGisHash()
        {

            string magic = ":" + GetData(UserId, 50, EndCursor);
            byte[] encoded = Encoding.UTF8.GetBytes(magic);
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(encoded);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
            
        }
        public async Task<Response> GetResponseString(string appendUrl, CookieContainer cookies= null,Dictionary<string,string> headers = null)
        {
            Console.WriteLine("URL : " + BASE_URL + appendUrl);
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create(BASE_URL + appendUrl);
            request.Method = WebRequestMethods.Http.Get;
            request.UserAgent = USER_AGENT;
            if (cookies != null)
                request.CookieContainer = cookies;
            if(headers != null)
                foreach (var item in headers)
                {
                    request.Headers.Add(item.Key, item.Value);
                }

            using (var response = await request.GetResponseAsync())
            {
                
                using (var responseStream = response.GetResponseStream())
                {
                    using (var streamReader = new StreamReader(responseStream))
                    {
                        return new Response( streamReader.ReadToEnd(),response.Headers,request.CookieContainer);
                    }
                }
            }
            
        }
        public HashData ExtractHashQuery(string responseString)
        {
            string pattern = "<script.*?src=\"(.*?)\"";
            var matches = Regex.Matches(responseString, pattern,RegexOptions.Compiled);
            string jsFile = matches.Where(x => x.Value.Contains("ProfilePageContainer.js"))
                .Select(x => x.Groups[1].Value).FirstOrDefault();
            string url = BASE_URL + jsFile.Substring(1);
            WebClient client = new WebClient();
            string resp =client.DownloadString(url);
            string hashPattern = "queryId:\"(.*?)\"";
            var hashMatches = Regex.Matches(resp, hashPattern, RegexOptions.Compiled);
            string hash = hashMatches[2].Groups[1].Value;
            string cursorPattern = "\"end_cursor\":\"(.*?)\"";
            var curosr = Regex.Match(responseString, cursorPattern, RegexOptions.Compiled).Groups[1].Value;
            return new HashData(hash,curosr);
        }
        
        public async Task InitialRequest()
        {
            
           
            Dictionary<string,string> headers = new Dictionary<string, string>();
            CookieContainer cookies = new CookieContainer();
            headers.Add("Referer",BASE_URL);
            headers.Add("User-Agent",USER_AGENT);
            cookies.Add(new Cookie("ig_pr","1"){Domain = "instagram.com"});
           
            Response response = await GetResponseString(Username, cookies, headers);
            var cookie = response.Cookies.GetCookies(new Uri("https://instagram.com",UriKind.Absolute));
            string csrfToken = cookie["csrftoken"].Value;
            string pattern = "<script type=\"text/javascript\">window._sharedData = (.*?);</script>";
            var match = Regex.Match(response.ResponseBody, pattern);
            HashData hashData = ExtractHashQuery(response.ResponseBody);
            this.CsrfToken = csrfToken;
            this.HashQuery = hashData.HashId;
            this.EndCursor = hashData.EndCursor;

        }
        public async Task<string> GetUserId()
        {
            var response = await GetResponseString(GET_ID_URL + Username);
            string resp = response.ResponseBody;
            dynamic x = JsonConvert.DeserializeObject(resp);
            return x.users[0].user.pk.ToString();
        }
        public async Task<List<Media>> GetUserMedia()
        {
            string url = string.Format(GRAPHQL, HashQuery,GetData(UserId,10,""));
            Console.WriteLine(url);
            Console.WriteLine(HashQuery);
            Console.WriteLine(GetData(UserId,50,EndCursor));
            Console.WriteLine("Hash " + GetGisHash());
            Console.WriteLine("CSRF " + CsrfToken);
            Dictionary<string,string> headers = new Dictionary<string, string>();
            CookieContainer cookies = new CookieContainer();
            headers.Add("x-instagram-gis", GetGisHash());
            headers.Add("Referer",BASE_URL);
            headers.Add("User-Agent",USER_AGENT);
            headers.Add("X-CSRFToken",CsrfToken);
            headers.Add("X-Requested-With","XMLHttpRequest");
            cookies.Add(new Cookie("csrftoken",CsrfToken){Domain = "instagram.com"});
            cookies.Add(new Cookie("ig_pr","1"){Domain = "instagram.com"});
            Response response = await GetResponseString(url,cookies,headers);
            dynamic json = JsonConvert.DeserializeObject<dynamic>(response.ResponseBody);
            var nodes = json.data.user.edge_owner_to_timeline_media.edges;
            List<Media> mediaList = new List<Media>();
            foreach (var node in nodes)
            {
               string shortcode = node.node.shortcode.ToString();
               Console.WriteLine("shortcode : " + shortcode);
               Media media = await GetMediaDetail(shortcode);
               mediaList.Add(media);
            }


            return mediaList;


        }

        

        private async Task<Media> GetMediaDetail(string shortCode)
        {
            Dictionary<string,string> headers = new Dictionary<string, string>();
            CookieContainer cookies = new CookieContainer();
            headers.Add("x-instagram-gis", GetGisHash());
            headers.Add("Referer",BASE_URL);
            headers.Add("User-Agent",USER_AGENT);
            headers.Add("X-CSRFToken",CsrfToken);
            headers.Add("X-Requested-With","XMLHttpRequest");
            cookies.Add(new Cookie("csrftoken",CsrfToken){Domain = "instagram.com"});
            cookies.Add(new Cookie("ig_pr","1"){Domain = "instagram.com"});
            Response response = await GetResponseString(string.Format(MediaUrlFormat, shortCode));
            dynamic json = JsonConvert.DeserializeObject<dynamic>(response.ResponseBody);
            string text = json.graphql.shortcode_media.edge_media_to_caption.edges[0].node.text.ToString();
            string displayUrl = json.graphql.shortcode_media.display_url;
            List<string> sideUrls = new List<string>();
            //var t = json.graphql.shortcode_media.edge_sidecar_to_children;
            if (json.graphql.shortcode_media.edge_sidecar_to_children != null)
            {
                foreach (var node in json.graphql.shortcode_media.edge_sidecar_to_children.edges)
                {
                    sideUrls.Add(node.node.display_url.ToString());
                }
            }
            
            return new Media(displayUrl,text,sideUrls);
        }
        
    }
}