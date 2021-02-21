// TODO: Paging for PoliticalCartoons.com

using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ComicViewer
{
    class Program
    {
        private readonly HttpClient _HttpClient = new HttpClient();
        private List<ComicData> xkcdList = new List<ComicData>();
        private List<ComicData> comicList = new List<ComicData>();
        private List<ComicData> comicKingdomList = new List<ComicData>();
        private string lastViewedFile;
        private Dictionary<string, string> lastViewed = new Dictionary<string, string>();
        private Dictionary<string, string> lastViewedUpdated = new Dictionary<string, string>();
        public List<PoliticalCartoon> politicalCartoons = new List<PoliticalCartoon>();
        private string newestDate;

        static async Task Main(string[] args)
        {
            try
            {
                int daysBack = 0;
                if (args.Length == 1)
                {
                    int.TryParse(args[0], out daysBack);
                }
                Program program = new Program();
                await program.Run(daysBack);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        private async Task Run(int daysBack = 0) // 0 for don't override last viewed date
        {
            lastViewedFile = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            lastViewedFile = Path.Combine(lastViewedFile, "Kohler");
            Directory.CreateDirectory(lastViewedFile);
            lastViewedFile = Path.Combine(lastViewedFile, "ComicViewer.json");
            if (File.Exists(lastViewedFile))
            {
                using(StreamReader reader = new StreamReader(lastViewedFile))
                {
                    string data = reader.ReadToEnd();
                    lastViewed = JsonSerializer.Deserialize<Dictionary<string, string>>(data);
                    lastViewedUpdated = JsonSerializer.Deserialize<Dictionary<string, string>>(data);
                }
            }
            IConfiguration config = new ConfigurationBuilder()
                            .AddJsonFile("comicviewer.json", true, true)
                            .Build();

            var configuration = new Comics();
            config.GetSection("Comics").Bind(configuration);
            foreach (var comic in configuration.Website)
            {
                DateTime lastDate;
                string data = string.Empty;
                switch (comic.Type)
                {
                    case "xkcd":
                        {
                            string lastComic = string.Empty;
                            if (lastViewed.ContainsKey("xkcd"))
                            {
                                lastComic = lastViewed["xkcd"];
                            }
                            if (!await ProcessXkcd(comic.Name, lastComic, daysBack))
                            {
                                Console.WriteLine($"Unable to download {comic.Name}");
                                continue;
                            }
                            string lastViewedComic = string.Empty;
                            if (lastViewedUpdated.ContainsKey("xkcd"))
                            {
                                lastViewedComic = lastViewedUpdated["xkcd"];
                            }
                            if (string.Compare(newestDate, lastViewedComic) > 0)
                            {
                                lastViewedUpdated["xkcd"] = newestDate;
                            }
                        }
                        break;
                    case "comicskingdom":
                        {
                            data = await GetResponse(comic.Name);
                            if (string.IsNullOrWhiteSpace(data))
                            {
                                Console.WriteLine($"Unable to download {comic.Name}");
                                continue;
                            }
                            if (lastViewed.ContainsKey("comicskingdom"))
                            {
                                newestDate = lastViewed["comicskingdom"];
                                lastDate = DateTime.Parse(newestDate);
                            }
                            else
                            {
                                lastDate = DateTime.Today.Date - new TimeSpan(14, 0, 0, 0);
                                newestDate = lastDate.ToString("yyyy-MM-dd");
                            }
                            if (daysBack > 0)
                            {
                                lastDate = DateTime.Now.Date - new TimeSpan(daysBack, 0, 0, 0);
                            }
                            await ProcessComicsKingdom(comic.Name, data, lastDate);
                            string lastViewedDate = string.Empty;
                            if (lastViewedUpdated.ContainsKey("comicskingdom"))
                            {
                                lastViewedDate = lastViewedUpdated["comicskingdom"];
                            }
                            if (string.Compare(newestDate, lastViewedDate) > 0)
                            {
                                lastViewedUpdated["comicskingdom"] = newestDate;
                            }
                        }
                        break;
                    case "gocomics":
                        {
                            data = await GetResponse(comic.Name);
                            if (string.IsNullOrWhiteSpace(data))
                            {
                                Console.WriteLine($"Unable to download {comic.Name}");
                                continue;
                            }
                            if (lastViewed.ContainsKey("gocomics"))
                            {
                                newestDate = lastViewed["gocomics"];
                                lastDate = DateTime.Parse(newestDate);
                            }
                            else
                            {
                                lastDate = DateTime.Today.Date - new TimeSpan(14, 0, 0, 0);
                                newestDate = lastDate.ToString("yyyy/MM/dd");
                            }
                            if (daysBack > 0)
                            {
                                lastDate = DateTime.Now.Date - new TimeSpan(daysBack, 0, 0, 0);
                            }
                            await ProcessGoComic(comic.Name, data, lastDate);
                            string lastViewedDate = string.Empty;
                            if (lastViewedUpdated.ContainsKey("gocomics"))
                            {
                                lastViewedDate = lastViewedUpdated["gocomics"];
                            }
                            if (string.Compare(newestDate, lastViewedDate) > 0)
                            {
                                lastViewedUpdated["gocomics"] = newestDate;
                            }
                        }
                        break;
                    case "politicalcartoons":
                        {
                            data = await GetResponse(comic.Name);
                            if (string.IsNullOrWhiteSpace(data))
                            {
                                Console.WriteLine($"Unable to download {comic.Name}");
                                continue;
                            }
                            if (lastViewed.ContainsKey("politicalcartoons"))
                            {
                                newestDate = lastViewed["politicalcartoons"];
                                lastDate = DateTime.Parse(newestDate);
                            }
                            else
                            {
                                lastDate = DateTime.Today.Date - new TimeSpan(14, 0, 0, 0);
                                newestDate = lastDate.ToString("yyyy/MM/dd");
                            }
                            if (daysBack > 0)
                            {
                                lastDate = DateTime.Now.Date - new TimeSpan(daysBack, 0, 0, 0);
                            }
                            await ProcessPoliticalCartoons(comic.Name, data, lastDate);
                            string lastViewedDate = string.Empty;
                            if (lastViewedUpdated.ContainsKey("politicalcartoons"))
                            {
                                lastViewedDate = lastViewedUpdated["politicalcartoons"];
                            }
                            if (string.Compare(newestDate, lastViewedDate) > 0)
                            {
                                lastViewedUpdated["politicalcartoons"] = newestDate;
                            }
                        }
                        break;
                }
            }
            bool first = true;
            string htmlFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Kohler", "comics.html");
            using (StreamWriter writer = new StreamWriter(htmlFile))
            {
                writer.WriteLine("<html>");
                writer.WriteLine("<body>");
                if (xkcdList.Count > 0 || comicKingdomList.Count > 0 || comicList.Count > 0 || politicalCartoons.Count > 0)
                {
                    foreach (var comic in xkcdList)
                    {
                        writer.WriteLine($"  {comic.Title}<br/>");
                        writer.WriteLine($"  <a href=\"{comic.Url}\">");
                        writer.WriteLine($"  <img src=\"{comic.Image}\" loading=\"lazy\">");
                        writer.WriteLine($"  </a><br/>");
                        first = false;
                    }
                    foreach (var comic in comicKingdomList)
                    {
                        if (!first && comic.First)
                        {
                            writer.WriteLine("  <hr/>");
                        }
                        writer.WriteLine($"  {comic.Title}<br/>");
                        writer.WriteLine($"  <a href=\"{comic.Url}\">");
                        writer.WriteLine($"  <img src=\"{comic.Image}\" loading=\"lazy\">");
                        writer.WriteLine($"  </a><br/>");
                        first = false;
                    }
                    foreach (var comic in comicList)
                    {
                        if (!first && comic.First)
                        {
                            writer.WriteLine("  <hr/>");
                        }
                        writer.WriteLine($"  {comic.Title}<br/>");
                        writer.WriteLine($"  <a href=\"{comic.Url}\">");
                        writer.WriteLine($"  <img src=\"{comic.Image}\" loading=\"lazy\">");
                        writer.WriteLine($"  </a><br/>");
                        first = false;
                    }
                    string author = string.Empty;
                    foreach (var politicalCartoon in politicalCartoons)
                    {
                        if (!first && author != politicalCartoon.Author)
                        {
                            writer.WriteLine("  <hr/>");
                        }
                        if (author != politicalCartoon.Author)
                        {
                            author = politicalCartoon.Author;
                            writer.WriteLine($"<h2>{politicalCartoon.Author}</h2>");
                        }
                        writer.WriteLine($"  {politicalCartoon.Title} {politicalCartoon.Date}<br/>");
                        writer.WriteLine($"  <a href=\"{politicalCartoon.Url}\">");
                        writer.WriteLine($"  <img src=\"{politicalCartoon.Image}\" loading=\"lazy\">");
                        writer.WriteLine($"  </a><br/>");
                        first = false;
                    }
                }
                else
                {
                    writer.WriteLine("<h2>No comics for today</h2>");
                }
                writer.WriteLine("</body>");
                writer.WriteLine("</html>");
            }

            try
            {
                string json = JsonSerializer.Serialize(lastViewedUpdated);
                using (StreamWriter writer = new StreamWriter(lastViewedFile))
                {
                    writer.WriteLine(json);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            Process process = new Process();
            process.StartInfo.FileName = configuration.Browser;
            process.StartInfo.Arguments = htmlFile;
            process.Start();
        }

        private async Task<string> GetResponse(string url)
        {
            try
            {
                Debug.WriteLine($"Downloading {url}...");
                var webRequest = (HttpWebRequest)WebRequest.Create(url);
                webRequest.Method = WebRequestMethods.Http.Get;
                webRequest.KeepAlive = true;
                webRequest.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
                webRequest.Headers.Add("Accept-Encoding", "gzip,deflate");
                webRequest.Headers.Add("Accept-Language", "en-US,en;q=0.5");
                webRequest.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:82.0) Gecko/20100101 Firefox/82.0");
                webRequest.Referer = url;
                using (HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    using (Stream stream = GetStreamForResponse(webResponse, 1000))
                    {
                        using (var streamReader = new StreamReader(stream))
                        {
                            return await streamReader.ReadToEndAsync().ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"URL: {url}");
                Debug.WriteLine($"Error: {ex.Message}");
            }
            return string.Empty;
        }

        private static Stream GetStreamForResponse(HttpWebResponse webResponse, int readTimeOut)
        {
            Stream stream;
            switch (webResponse.ContentEncoding?.ToUpperInvariant())
            {
                case "GZIP":
                    stream = new GZipStream(webResponse.GetResponseStream(), CompressionMode.Decompress);
                    break;
                case "DEFLATE":
                    stream = new DeflateStream(webResponse.GetResponseStream(), CompressionMode.Decompress);
                    break;
                default:
                    stream = webResponse.GetResponseStream();
                    break;
            }
            return stream;
        }

        #region ComicsKingdom
        private async Task ProcessComicsKingdom(string comicUrl, string data, DateTime dtLastDate)
        {
            string lastDate = dtLastDate.ToString("yyyy-MM-dd");
            int index = comicUrl.LastIndexOf("/");
            if (index != -1)
            {
                string comic = comicUrl.Substring(index);
                comicUrl = comicUrl.Replace(comic, string.Empty);
                comic += "/";
                index = data.IndexOf(comic);
                if (index != -1)
                {
                    int index2 = data.IndexOf('\'', index);
                    if (index2 != -1)
                    {
                        string comicDate = data.Substring(index, index2 - index);
                        comicDate = comicDate.Replace(comic, string.Empty);
                        await ProcessProcessComicsKingdomDate(comicUrl, comic, comicDate, lastDate);
                    }
                }
            }
        }

        private async Task ProcessProcessComicsKingdomDate(string comicUrl, string comic, string comicDate, string lastDate)
        {
            bool first = true;
            while (string.Compare(comicDate, lastDate) > 0)
            {
                try
                {
                    string data = await GetResponse($"{comicUrl}{comic}{comicDate}");
                    string title = GetElement(data, "<title>", "</title>", true).TrimEnd();
                    string image = GetElement(data, "<meta property=\"og:image\" content=\"", "\" />", true);
                    if (!string.IsNullOrWhiteSpace(image))
                    {
                        ComicData comicData = new ComicData
                        {
                            First = first,
                            Image = image,
                            Title = title,
                            Url = $"{comicUrl}{comic}{comicDate}"
                        };
                        first = false;
                        Console.WriteLine($"{comic}{comicDate}");
                        comicKingdomList.Add(comicData);
                        if (string.Compare(comicDate.Replace('-','/'), newestDate) > 0)
                        {
                            newestDate = comicDate.Replace('-', '/');
                        }
                        int pos = data.IndexOf("date-slug");
                        comicDate = string.Empty;
                        if (pos != -1)
                        {
                            pos = data.IndexOf("\"", pos);
                            if (pos != -1)
                            {
                                string temp = data.Substring(pos + 1);
                                pos = temp.IndexOf("\"");
                                if (pos != -1)
                                {
                                    comicDate = temp.Substring(0, pos);
                                }
                            }
                        }
                        if (comicDate == string.Empty)
                            break;
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
        }
        #endregion

        #region GoComic
        private async Task ProcessGoComic(string comicUrl,string data, DateTime dtLastDate)
        {
            string lastDate = dtLastDate.ToString("yyyy/MM/dd");
            int index = comicUrl.LastIndexOf("/");
            if (index != -1)
            {
                string comic = comicUrl.Substring(index);
                comicUrl = comicUrl.Replace(comic, string.Empty);
                comic += "/";
                index = data.IndexOf(comic);
                if (index != -1)
                {
                    int index2 = data.IndexOf('"', index);
                    if (index2 != -1)
                    {
                        string comicDate = data.Substring(index, index2 - index);
                        comicDate = comicDate.Replace(comic, string.Empty);
                        await ProcessGoComicDate(comicUrl, comic, comicDate, lastDate);
                    }
                }
            }
        }

        private async Task ProcessGoComicDate(string comicUrl, string comic, string comicDate, string lastDate)
        {
            bool first = true;
            while(string.Compare(comicDate, lastDate) > 0)
            {
                try
                {
                    Console.WriteLine($"{comic}{comicDate}");
                    string data = await GetResponse($"{comicUrl}{comic}{comicDate}");
                    string title = GetElement(data, "<title>", "</title>", true);
                    string image = GetElement(data, "<meta property=\"og:image\" content=\"", "\" />", true);
                    if (!string.IsNullOrWhiteSpace(image))
                    {
                        ComicData comicData = new ComicData
                        {
                            First = first,
                            Image = image,
                            Title = title,
                            Url = $"{comicUrl}{comic}{comicDate}"
                        };
                        comicList.Add(comicData);
                        if (string.Compare(comicDate, newestDate) > 0)
                        {
                            newestDate = comicDate;
                        }
                        first = false;
                        string date = GetElement(data, "<a role='button' href='", "'", true);
                        date = date.Replace(comic, string.Empty);
                        DateTime dt = DateTime.Parse(date);
                        if (DateTime.Parse(comicDate) - DateTime.Parse(date) > new TimeSpan(30, 0, 0, 0))
                        {
                            int index = data.IndexOf("<a role='button' href='");
                            if (index != -1)
                            {
                                string temp = data.Substring(index + 24);
                                date = GetElement(temp, "<a role='button' href='", "'", true);
                                date = date.Replace(comic, string.Empty);
                            }
                        }
                        comicDate = date;
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
        }
        #endregion

        #region PoliticalCartoons
        private async Task ProcessPoliticalCartoons(string comicUrl, string data, DateTime dtLastDate)
        {
            string author = string.Empty;
            string[] lines = data.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                int pos = l.IndexOf("<h1 class=\"title\">");
                if (pos != -1)
                {
                    l = l.Substring(pos + 18);
                    pos = l.IndexOf('<');
                    if (pos != -1)
                    {
                        author = l.Substring(0, pos);
                        break;
                    }
                }
            }
            await ProcessPoliticalCartoons(comicUrl, lines, dtLastDate, author);
        }

        private async Task ProcessPoliticalCartoons(string comicUrl, string[] lines, DateTime dtLastDate, string author)
        {
            bool foundLastDate = false;
            bool foundDate = false;
            string tmp;
            for (var i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                int pos = l.IndexOf("<span class=\"cartoon-published text-right\">");
                if (pos != -1)
                {
                    tmp = l.Substring(pos + 43);
                    pos = tmp.IndexOf('<');
                    if (pos != -1)
                    {
                        tmp = tmp.Substring(0, pos);
                        var dateParts = tmp.Split("/");
                        DateTime dateTime = new DateTime(2000 + Convert.ToInt32(dateParts[2]), Convert.ToInt32(dateParts[0]), Convert.ToInt32(dateParts[1]));
                        foundDate = true;
                        if (dateTime > dtLastDate)
                        {
                            string comicDate = dateTime.ToString("yyyy/MM/dd");
                            if (string.Compare(comicDate, newestDate) > 0)
                            {
                                newestDate = comicDate;
                            }
                            l = lines[i - 1];
                            pos = l.IndexOf('#');
                            if (pos != -1)
                            {
                                l = l.Substring(pos + 1);
                                pos = l.IndexOf('<');
                                if (pos != -1)
                                {
                                    string sku = l.Substring(0, pos);
                                    var cartoon = new PoliticalCartoon
                                    {
                                        Author = author,
                                        Date = dateTime.ToString("MM/dd/yyyy"),
                                        Url = "https://politicalcartoons.com/sku/" + sku,
                                    };
                                    l = lines[i - 8];
                                    cartoon.Title = l.Replace(" </div>", "");
                                    string data = await GetResponse(cartoon.Url);
                                    string tag = $"<IMG ID=\"{sku}\" src=\"";
                                    pos = data.IndexOf(tag);
                                    if (pos != -1)
                                    {
                                        data = data.Substring(pos + tag.Length);
                                        pos = data.IndexOf('"');
                                        if (pos != -1)
                                        {
                                            cartoon.Image = "https:" + data.Substring(0,pos);
                                            Console.WriteLine($"{cartoon.Author} {cartoon.Date}");
                                            politicalCartoons.Add(cartoon);
                                        }
                                    }
                                }

                            }
                            else if (dateTime <= dtLastDate)
                            {
                                foundLastDate = true;
                            }
                        }
                    }
                }
                if (!foundLastDate && foundDate)
                {
                    // If we are correctly processing the HTML file and finding comic dates, but didn't find the last one, go to next page, if possible and continue search.

                }
            }
        }
        #endregion

        #region XKCD
        private async Task<bool> ProcessXkcd(string comicUrl, string lastComic, int maxComics)
        {
            string data = string.Empty;
            bool first = true;
            if (string.IsNullOrEmpty(lastComic))
            {
                Console.WriteLine(comicUrl);
                data = await GetResponse(comicUrl);
                int pos = data.IndexOf("rel=\"prev\"");
                if (pos == -1)
                    return false;
                pos = data.IndexOf("href=\"", pos + 1);
                if (pos == -1)
                    return false;
                int pos2 = data.IndexOf("\"",pos + 6);
                newestDate = data.Substring(pos + 7, pos2 - pos - 8);
                string title = GetElement(data, "<title>", "</title>", true);
                string image = GetElement(data, "<meta property=\"og:image\" content=\"", "\">", true).Replace("_2x","");
                if (!string.IsNullOrWhiteSpace(image))
                {
                    ComicData comicData = new ComicData
                    {
                        Image = image,
                        Title = title,
                        Url = comicUrl + newestDate
                    };
                    xkcdList.Add(comicData);
                    Console.WriteLine(comicData.Url);
                    return true;
                }
            }
            int comicIndex;
            if (!int.TryParse(lastComic, out comicIndex))
                return false;
            comicIndex++;

            data = await GetResponse($"{comicUrl}/{comicIndex}/");
            if (string.IsNullOrWhiteSpace(data))
                return false;
            int comicsFound = 0;
            while (true)
            {
                comicsFound++;
                string title = GetElement(data, "<title>", "</title>", true);
                string image = GetElement(data, "<meta property=\"og:image\" content=\"", "\">", true).Replace("_2x", "");
                if (!string.IsNullOrWhiteSpace(image))
                {
                    ComicData comicData = new ComicData
                    {
                        First = first,
                        Image = image,
                        Title = title,
                        Url = $"{comicUrl}/{comicIndex}/"
                    };
                    xkcdList.Insert(0,comicData);
                    newestDate = comicIndex.ToString();
                    Console.WriteLine(comicData.Url);
                    first = false;
                }
                else
                {
                    break;
                }

                if (comicsFound == maxComics)
                    break;

                if (data.IndexOf("\"#\"") != -1)
                    break;
                int pos = data.IndexOf("rel=\"next\"");
                if (pos == -1)
                    break;
                pos = data.IndexOf("href=\"", pos + 1);
                if (pos == -1)
                    break;
                int pos2 = data.IndexOf("\"", pos + 6);
                lastComic = data.Substring(pos + 7, pos2 - pos - 8);
                if (lastComic == "#")
                    break;
                if (!int.TryParse(lastComic, out comicIndex))
                    break;
                data = await GetResponse($"{comicUrl}/{comicIndex}/");
                if (string.IsNullOrWhiteSpace(data))
                    break;
            }
            return true;
        }
        #endregion

        private string GetElement(string html, string openTag, string closeTag, bool removeFromStart)
        {
            string result = string.Empty;
            int pos = html.IndexOf(openTag);
            if (pos != -1)
            {
                int pos2 = html.IndexOf(closeTag, pos + 1 + openTag.Length);
                if (pos2 != -1)
                {
                    result = html.Substring(pos + openTag.Length, pos2 - (pos + openTag.Length));
                }
                if (removeFromStart)
                {
                    html = html.Remove(0, pos2 + closeTag.Length);
                }
            }
            return result;
        }

    }

    public class ComicData
    {
        public bool First { get; set; } // First comic for this type
        public string Title { get; set; }
        public string Image { get; set; }
        public string Url { get; set; }
    }

    public class Website
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }

    public class Comics
    {
        public string Browser { get; set; }
        public Website[] Website { get; set; }
    }
    public class PoliticalCartoon
    {
        public string Author { get; set; }
        public string Date { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string Image { get; set; }
    }
}
