using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace InstagramScraperSharp
{
    public class ClientDownloader
    {
        private const string USER_AGENT =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36";

        private WebClient webClient;
        private List<string> Urls;
        public List<MemoryStream> Streams { private set; get; }
        public ClientDownloader(List<string> urls)
        {
            Urls = urls;
            webClient = new WebClient();
            webClient.Headers.Add("User-Agent",USER_AGENT);
        }

        public ClientDownloader(string url)
        {
            Urls = new List<string>(){url};
            webClient = new WebClient();
            webClient.Headers.Add("User-Agent",USER_AGENT);
        }

        public void DownloadFilesAsync()
        {
            List<MemoryStream> streams = new List<MemoryStream>(Urls.Count);
            
            foreach (var url in Urls)
            {
                var uri = new Uri(url, UriKind.Absolute);
                byte[] bytes = webClient.DownloadData(uri);
                streams.Add(new MemoryStream(bytes));


            }

            Streams = streams;
        }

        public async Task SaveToLocal()
        {
          
            foreach (var stream in Streams)
            {
                using (FileStream fileStream = new FileStream(Guid.NewGuid().ToString().Replace("-","")+".jpg",FileMode.Create,FileAccess.Write))
                {
                    await stream.CopyToAsync(fileStream);
                }

               
            }
        }
    }
}