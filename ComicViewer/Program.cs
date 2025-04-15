// TODO: Paging for PoliticalCartoons.com

using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace ComicViewer
{
    class Program
    {
        private readonly List<ComicData> xkcdList = new List<ComicData>();
        private readonly List<ComicData> comicList = new List<ComicData>();
        private readonly List<ComicData> comicKingdomList = new List<ComicData>();
        private string lastViewedFile;
        private string lastViewedFileLocal;
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
                await Console.Out.WriteLineAsync("Error: " + ex.Message);
            }
        }

        private async Task Run(int daysBack = 0) // 0 for don't override last viewed date
        {
            (Comics configuration, Website[] websites) = GetComicList();

            GetLastViewed(configuration);

            foreach (var comic in websites)
            {
                DateTime lastDate;
                string data;
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
                                await Console.Out.WriteLineAsync($"Unable to download {comic.Name}");
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
                                await Console.Out.WriteLineAsync($"Unable to download {comic.Name}");
                                continue;
                            }
                            if (lastViewed.ContainsKey(comic.Name))
                            {
                                newestDate = lastViewed[comic.Name];
                                lastDate = DateTime.Parse(newestDate, CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                lastDate = DateTime.Today.Date - new TimeSpan(1, 0, 0, 0);
                                newestDate = lastDate.ToString("yyyy-MM-dd");
                            }
                            if (daysBack > 0)
                            {
                                lastDate = DateTime.Now.Date - new TimeSpan(daysBack, 0, 0, 0);
                            }
                            await ProcessComicsKingdom(comic.Name, data, lastDate);
                            string lastViewedDate = string.Empty;
                            if (lastViewedUpdated.ContainsKey(comic.Name))
                            {
                                lastViewedDate = lastViewedUpdated[comic.Name];
                            }
                            if (string.Compare(newestDate, lastViewedDate) > 0)
                            {
                                lastViewedUpdated[comic.Name] = newestDate;
                            }
                        }
                        break;
                    case "gocomics":
                        {
                            if (lastViewed.ContainsKey(comic.Name))
                            {
                                newestDate = lastViewed[comic.Name];
                                lastDate = DateTime.Parse(newestDate, CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                lastDate = DateTime.Today.Date - new TimeSpan(1, 0, 0, 0);
                                newestDate = lastDate.ToString("yyyy/MM/dd");
                            }
                            if (daysBack > 0)
                            {
                                lastDate = DateTime.Now.Date - new TimeSpan(daysBack, 0, 0, 0);
                            }
                            await ProcessGoComic(comic.Name, lastDate);
                            string lastViewedDate = string.Empty;
                            if (lastViewedUpdated.ContainsKey(comic.Name))
                            {
                                lastViewedDate = lastViewedUpdated[comic.Name];
                            }
                            if (string.Compare(newestDate, lastViewedDate) > 0)
                            {
                                lastViewedUpdated[comic.Name] = newestDate;
                            }
                        }
                        break;
                    case "politicalcartoons":
                        {
                            data = await GetResponse(comic.Name);
                            if (string.IsNullOrWhiteSpace(data))
                            {
                                await Console.Out.WriteLineAsync($"Unable to download {comic.Name}");
                                continue;
                            }
                            if (lastViewed.ContainsKey(comic.Name))
                            {
                                newestDate = lastViewed[comic.Name];
                                lastDate = DateTime.Parse(newestDate, CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                lastDate = DateTime.Today.Date - new TimeSpan(1, 0, 0, 0);
                                newestDate = lastDate.ToString("yyyy/MM/dd");
                            }
                            if (daysBack > 0)
                            {
                                lastDate = DateTime.Now.Date - new TimeSpan(daysBack, 0, 0, 0);
                            }
                            await ProcessPoliticalCartoons(data, lastDate);
                            string lastViewedDate = string.Empty;
                            if (lastViewedUpdated.ContainsKey(comic.Name))
                            {
                                lastViewedDate = lastViewedUpdated[comic.Name];
                            }
                            if (string.Compare(newestDate, lastViewedDate) > 0)
                            {
                                lastViewedUpdated[comic.Name] = newestDate;
                            }
                        }
                        break;
                }
            }
            bool first = true;
            string htmlFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Kohler", "comics.html");
            using (StreamWriter writer = new StreamWriter(htmlFile))
            {
                await writer.WriteLineAsync("<html>");
                await writer.WriteLineAsync("<body>");
                if (xkcdList.Count > 0 || comicKingdomList.Count > 0 || comicList.Count > 0 || politicalCartoons.Count > 0)
                {
                    foreach (var comic in xkcdList)
                    {
                        await writer.WriteLineAsync($"  {comic.Title}<br/>");
                        await writer.WriteLineAsync($"  <a href=\"{comic.Url}\">");
                        await writer.WriteLineAsync($"  <img src=\"{comic.Image}\" loading=\"lazy\">");
                        await writer.WriteLineAsync($"  </a><br/>");
                        first = false;
                    }
                    foreach (var comic in comicKingdomList)
                    {
                        if (!first && comic.First)
                        {
                            await writer.WriteLineAsync("  <hr/>");
                        }
                        await writer.WriteLineAsync($"  {comic.Title}<br/>");
                        await writer.WriteLineAsync($"  <a href=\"{comic.Url}\">");
                        await writer.WriteLineAsync($"  <img src=\"{comic.Image}\" loading=\"lazy\">");
                        await writer.WriteLineAsync($"  </a><br/>");
                        first = false;
                    }
                    foreach (var comic in comicList)
                    {
                        if (!first && comic.First)
                        {
                            await writer.WriteLineAsync("  <hr/>");
                        }
                        await writer.WriteLineAsync($"  {comic.Title}<br/>");
                        await writer.WriteLineAsync($"  <a href=\"{comic.Url}\">");
                        await writer.WriteLineAsync($"  <img src=\"{comic.Image}\" loading=\"lazy\">");
                        await writer.WriteLineAsync($"  </a><br/>");
                        first = false;
                    }
                    string author = string.Empty;
                    foreach (var politicalCartoon in politicalCartoons)
                    {
                        if (!first && author != politicalCartoon.Author)
                        {
                            await writer.WriteLineAsync("  <hr/>");
                        }
                        if (author != politicalCartoon.Author)
                        {
                            author = politicalCartoon.Author;
                            await writer.WriteLineAsync($"<h2>{politicalCartoon.Author}</h2>");
                        }
                        await writer.WriteLineAsync($"  {politicalCartoon.Title} {politicalCartoon.Date}<br/>");
                        await writer.WriteLineAsync($"  <a href=\"{politicalCartoon.Url}\">");
                        await writer.WriteLineAsync($"  <img src=\"{politicalCartoon.Image}\" loading=\"lazy\">");
                        await writer.WriteLineAsync($"  </a><br/>");
                        first = false;
                    }
                }
                else
                {
                    await writer.WriteLineAsync("<h2>No comics for today</h2>");
                }
                await writer.WriteLineAsync("</body>");
                await writer.WriteLineAsync("</html>");
            }

            try
            {
                string json = JsonSerializer.Serialize(lastViewedUpdated);
                using (StreamWriter writer = new StreamWriter(lastViewedFile))
                {
                    await writer.WriteLineAsync(json);
                }
                if (lastViewedFile != lastViewedFileLocal)
                {
                    File.Copy(lastViewedFile, lastViewedFileLocal, true);
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

        private void GetLastViewed(Comics configuration)
        {
            lastViewedFile = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            lastViewedFile = Path.Combine(lastViewedFile, "Kohler");
            Directory.CreateDirectory(lastViewedFile);
            lastViewedFile = Path.Combine(lastViewedFile, "ComicViewer.json");
            lastViewedFileLocal = lastViewedFile;
            if (File.Exists(lastViewedFile))
            {
                using (StreamReader reader = new StreamReader(lastViewedFile))
                {
                    string data = reader.ReadToEnd();
                    lastViewed = JsonSerializer.Deserialize<Dictionary<string, string>>(data);
                    lastViewedUpdated = JsonSerializer.Deserialize<Dictionary<string, string>>(data);
                }
            }

            if (!string.IsNullOrWhiteSpace(configuration.NetworkShare) && File.Exists(Path.Combine(configuration.NetworkShare, "ComicsLastViewed.json")))
            {
                lastViewedFile = Path.Combine(configuration.NetworkShare, "ComicsLastViewed.json");
                using (StreamReader reader = new StreamReader(lastViewedFile))
                {
                    string data = reader.ReadToEnd();
                    lastViewed = JsonSerializer.Deserialize<Dictionary<string, string>>(data);
                    lastViewedUpdated = JsonSerializer.Deserialize<Dictionary<string, string>>(data);
                }
            }
        }

        public (Comics configuration, Website[] websites) GetComicList()
        {
            IConfiguration config = new ConfigurationBuilder()
                            .AddJsonFile("comicviewer.json", true, true)
                            .Build();

            Comics configuration = new Comics();
            config.GetSection("Comics").Bind(configuration);
            var websites = configuration.Website;
            if (!string.IsNullOrWhiteSpace(configuration.NetworkShare) && File.Exists(Path.Combine(configuration.NetworkShare, "ComicList.json")))
            {
                string comicListFile = Path.Combine(configuration.NetworkShare, "ComicList.json");
                IConfiguration nsConfig = new ConfigurationBuilder()
                                .AddJsonFile(comicListFile, true, true)
                                .Build();

                var nsConfiguration = new Comics();
                nsConfig.GetSection("Comics").Bind(nsConfiguration);
                websites = nsConfiguration.Website;
            }
            return (configuration, websites);
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
                webRequest.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:137.0) Gecko/20100101 Firefox/137.0");
                webRequest.Referer = url;
                using (HttpWebResponse webResponse = (HttpWebResponse)await webRequest.GetResponseAsync())
                {
                    using (Stream stream = GetStreamForResponse(webResponse))
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

        private static Stream GetStreamForResponse(HttpWebResponse webResponse)
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
                    string data2 = data.Substring(index + comic.Length);
                    int index2 = data2.IndexOf('\"');
                    if (index2 != -1)
                    {
                        string comicDate = data2.Substring(0,index2);
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
                    string image = GetElement(data, "<meta property=\"og:image\" content=\"", "\"/>", true);
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
                        await Console.Out.WriteLineAsync(comic + " " + comicDate);
                        comicKingdomList.Add(comicData);
                        if (string.Compare(comicDate.Replace('-','/'), newestDate) > 0)
                        {
                            newestDate = comicDate.Replace('-', '/');
                        }
                        DateTime dateTime = DateTime.Parse(comicDate);
                        dateTime -= new TimeSpan(1, 0, 0, 0);
                        comicDate = dateTime.ToString("yyyy-MM-dd");
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
        private async Task ProcessGoComic(string comicUrl, DateTime dtLastDate)
        {
            IPlaywright playwright = await Playwright.CreateAsync();
            IBrowser browser = await playwright.Webkit.LaunchAsync();
            IPage page = null;
            try
            {
                try
                {
                    DateTime workingDate = dtLastDate;
                    string comic = comicUrl;
                    int index = comicUrl.LastIndexOf("/");
                    if (index != -1)
                    {
                        comic = comicUrl.Substring(index + 1);
                    }

                    bool first = true;
                    string pageUrl = $"{comicUrl}/{workingDate.ToString("yyyy/MM/dd")}";
                    if (dtLastDate == DateTime.Today.Date - new TimeSpan(1, 0, 0, 0)) // i.e. yesterday
                    {
                        pageUrl = comicUrl;
                    }
                    while (true)
                    {
                        if (page != null)
                        {
                            await page.CloseAsync();
                            page = null;
                        }
                        page = await browser.NewPageAsync();

                        await page.GotoAsync(pageUrl);
                        var content = await page.ContentAsync();
                        var doc = new HtmlDocument();
                        doc.LoadHtml(content);
                        var url = page.Url; // url retrieved
                        string comicDate = string.Empty;
                        if (url == comicUrl)
                        {
                            var buttons = doc.DocumentNode.SelectNodes(".//button").ToList();
                            comicDate = string.Empty;
                            foreach (var button in buttons)
                            {
                                comicDate = GetDate(button.InnerText);
                                if (!string.IsNullOrWhiteSpace(comicDate))
                                {
                                    break;
                                }
                            }
                        }
                        else
                        {
                            string temp = url.Replace(comicUrl + "/", string.Empty);
                            workingDate = DateTime.Parse(temp, CultureInfo.InvariantCulture);
                            comicDate = workingDate.ToString("yyyy/MM/dd");
                        }
                        if (string.IsNullOrWhiteSpace(comicDate))
                        {
                            await page.CloseAsync();
                            page = null;
                            playwright.Dispose();
                            playwright = null;
                            return;
                        }

                        DateTime currentDate = DateTime.Parse(comicDate, CultureInfo.InvariantCulture);
                        if (currentDate != dtLastDate)
                        {
                            // Ignore the last comic previously retrieved
                            index = content.IndexOf("https://featureassets.gocomics.com/assets/");
                            if (index != -1)
                            {
                                var imageContent = content.Substring(index);
                                index = imageContent.IndexOf("\"");
                                if (index != -1)
                                {
                                    imageContent = imageContent.Substring(0, index);
                                }
                                index = imageContent.IndexOf("?");
                                if (index != -1)
                                {
                                    imageContent = imageContent.Substring(0, index);
                                    var comicData = new ComicData
                                    {
                                        First = first,
                                        Image = imageContent,
                                        Title = comic + " " + currentDate.ToString("MM/dd/yyyy"),
                                        Url = pageUrl
                                    };
                                    comicList.Add(comicData);
                                    newestDate = comicDate;
                                    await Console.Out.WriteLineAsync($"{comic} {comicDate}");
                                    first = false;
                                }
                            }
                        }

                        if (url == comicUrl)
                            break;
                        pageUrl = GetNextDate(doc, comic, comicUrl, currentDate.ToString("yyyy/MM/dd"));
                        if (string.IsNullOrWhiteSpace(pageUrl))
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
            finally
            {
                if (page != null)
                {
                    await page.CloseAsync();
                    page = null;
                }
                if (browser != null)
                {
                    await browser.CloseAsync();
                    await browser.DisposeAsync();
                    browser = null;
                }
                if (playwright != null)
                {
                    playwright.Dispose();
                    playwright = null;
                }
            }
        }

        private static string GetNextDate(HtmlDocument doc, string comic, string comicUrl, string currentComicDate)
        {
            string pageUrl = string.Empty;
            var anchors = doc.DocumentNode.SelectNodes("//*[@href]").ToList();
            var comicHref = "/" + comic + "/";
            foreach (var anchor in anchors)
            {
                var hrefs = anchor.Attributes.Where(x => x.Name == "href" && x.Value.StartsWith(comicHref)).ToList();
                if (hrefs.Count > 0)
                {
                    string temp = hrefs[0].Value.Replace(comicHref, string.Empty);
                    if (string.Compare(temp, currentComicDate) > 0)
                    {
                        pageUrl = comicUrl + "/" + temp.Replace("/" + comic + "/", string.Empty);
                        break;
                    }
                }
            }
            if (pageUrl == string.Empty)
            {
                comicHref = "/" + comic;
                foreach (var anchor in anchors)
                {
                    var hrefs = anchor.Attributes.Where(x => x.Name == "href" && x.Value.StartsWith(comicHref)).ToList();
                    if (hrefs.Count > 0 && hrefs[0].Value == comicHref)
                    {
                        pageUrl = comicUrl;
                        break;
                    }
                }
            }
            return pageUrl;
        }

        public string GetDate(string innerText)
        {
            int index = innerText.IndexOf(',');
            if (index != -1)
            {
                var dayString = innerText.Substring(0, index);
                var daysOfWeek = Enum.GetValues(typeof(DayOfWeek));
                foreach(var dayOfWeek in daysOfWeek)
                {
                    if (dayString.ToLower().Contains(dayOfWeek.ToString().ToLower()))
                    {
                        dayString = innerText.Substring(dayString.Length + 2);
                        index = dayString.IndexOf(' ');
                        string monthString = dayString.Substring(0, index);
                        dayString = dayString.Substring(index + 1);
                        int day;
                        int.TryParse(dayString, out day);
                        int month = 0;
                        switch(monthString)
                        {
                            case "January":
                                month = 1;
                                break;
                            case "February":
                                month = 2;
                                break;
                            case "March":
                                month = 3;
                                break;
                            case "April":
                                month = 4;
                                break;
                            case "May":
                                month = 5;
                                break;
                            case "June":
                                month = 6;
                                break;
                            case "July":
                                month = 7;
                                break;
                            case "August":
                                month = 8;
                                break;
                            case "September":
                                month = 9;
                                break;
                            case "October":
                                month = 10;
                                break;
                            case "November":
                                month = 11;
                                break;
                            case "December":
                                month = 12;
                                break;
                        }

                        DateTime date = new DateTime(DateTime.Now.Year, month, day);
                        return date.ToString("yyyy/MM/dd");
                    }
                }
            }
            return string.Empty;
        }
        #endregion

        #region PoliticalCartoons
        private async Task ProcessPoliticalCartoons(string data, DateTime dtLastDate)
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
            await ProcessPoliticalCartoons(lines, dtLastDate, author);
        }

        private async Task ProcessPoliticalCartoons(string[] lines, DateTime dtLastDate, string author)
        {
            bool foundLastDate = false;
            bool foundDate = false;
            string tmp;
            for (var i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                int pos = l.IndexOf("<span class=\"cartoon-published");
                if (pos != -1) 
                {
                    pos = l.IndexOf(">");
                }
                if (pos != -1)
                {
                    tmp = l.Substring(pos + 1);
                    pos = tmp.IndexOf('<');
                    if (pos != -1)
                    {
                        tmp = tmp.Substring(0, pos);
                        var dateParts = tmp.Split("/");
                        DateTime dateTime = new DateTime(2000 + Convert.ToInt32(dateParts[2]), Convert.ToInt32(dateParts[0]), Convert.ToInt32(dateParts[1]), 0, 0, 0, DateTimeKind.Local);
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
                                    if (politicalCartoons.Exists(x => x.Url == cartoon.Url))
                                    {
                                        await Console.Out.WriteLineAsync($"Duplicate Cartoon: {cartoon.Url}");
                                    }
                                    else
                                    {
                                        string data = await GetResponse(cartoon.Url);
                                        string tag = $"<IMG ID=\"{sku}\" src=\"";
                                        pos = data.IndexOf(tag);
                                        if (pos != -1)
                                        {
                                            data = data.Substring(pos + tag.Length);
                                            pos = data.IndexOf('"');
                                            if (pos != -1)
                                            {
                                                cartoon.Image = "https:" + data.Substring(0, pos);
                                                await Console.Out.WriteLineAsync($"{cartoon.Author} {cartoon.Date}");
                                                politicalCartoons.Add(cartoon);
                                            }
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
                await Console.Out.WriteLineAsync(comicUrl);
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
                    await Console.Out.WriteLineAsync(comicData.Url);
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
                if (string.IsNullOrWhiteSpace(image)) 
                {
                    int index = data.IndexOf("<img src=\"//imgs.xkcd.com/comics/");
                    if (index > -1)
                    {
                        string temp = data.Substring(index + 10);
                        index = temp.IndexOf("\"");
                        image = temp.Substring(0, index);
                        image = "https:" + image;
                    }
                }

                if (!string.IsNullOrWhiteSpace(image))
                {
                    ComicData comicData = new ComicData
                    {
                        First = first,
                        Image = image,
                        Title = title,
                        Url = $"{comicUrl}/{comicIndex}/"
                    };
                    xkcdList.Add(comicData);
                    newestDate = comicIndex.ToString();
                    await Console.Out.WriteLineAsync(comicData.Url);
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
        public string NetworkShare { get; set; }
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
