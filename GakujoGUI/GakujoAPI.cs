﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace GakujoGUI
{
    internal class GakujoAPI
    {
        public Account account = new();
        public List<Report> reports = new();
        public List<Quiz> quizzes = new();
        public List<ClassContact> classContacts = new() { };
        public List<SchoolContact> schoolContacts = new() { };
        public List<ClassSharedFile> classSharedFiles = new() { };
        public List<SchoolSharedFile> schoolSharedFiles = new() { };
        public List<ClassResult> classResults = new() { };

        private CookieContainer cookieContainer = new();
        private HttpClientHandler httpClientHandler = new();
        private HttpClient httpClient = new();
        private HttpRequestMessage httpRequestMessage = new();
        private HttpResponseMessage httpResponse = new();
        private readonly HtmlDocument htmlDocument = new();

        private readonly string cookiesPath = Path.Combine(Environment.CurrentDirectory, "Cookies");
        private readonly string downloadPath = Path.Combine(Environment.CurrentDirectory, @"Download\");

        private readonly string schoolYear = "2021";
        private readonly int semesterCode = 2;
        private readonly string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.164 Safari/537.36 Edg/91.0.864.71";

        public static string GetJsonPath(string value)
        {
            return Path.Combine(Environment.CurrentDirectory, @"Json\" + value + ".json");
        }

        public GakujoAPI()
        {
            LoadCookies();
            LoadJson();
        }

        public void SetAccount(string userId, string passWord)
        {
            account = new Account() { UserId = userId, PassWord = passWord };
            SaveJson();
        }

        private void SaveCookies()
        {
            using Stream stream = File.Create(cookiesPath);
            BinaryFormatter binaryFormatter = new();
            binaryFormatter.Serialize(stream, cookieContainer);
        }

        private bool LoadCookies()
        {
            if (File.Exists(cookiesPath))
            {
                using (Stream stream = File.Open(cookiesPath, FileMode.Open))
                {
                    BinaryFormatter binaryFormatter = new();
                    cookieContainer = (CookieContainer)binaryFormatter.Deserialize(stream);
                }
                httpClientHandler = new HttpClientHandler
                {
                    AutomaticDecompression = ~DecompressionMethods.None,
                    CookieContainer = cookieContainer
                };
                httpClient = new HttpClient(httpClientHandler);
                return CheckConnection();
            }
            cookieContainer = new CookieContainer();
            return false;
        }

        private bool LoadJson()
        {
            if (File.Exists(GetJsonPath("Reports")))
            {
                reports = JsonConvert.DeserializeObject<List<Report>>(File.ReadAllText(GetJsonPath("Reports")))!;
            }
            if (File.Exists(GetJsonPath("Quizzes")))
            {
                quizzes = JsonConvert.DeserializeObject<List<Quiz>>(File.ReadAllText(GetJsonPath("Quizzes")))!;
            }
            if (File.Exists(GetJsonPath("ClassContacts")))
            {
                classContacts = JsonConvert.DeserializeObject<List<ClassContact>>(File.ReadAllText(GetJsonPath("ClassContacts")))!;
            }
            if (File.Exists(GetJsonPath("SchoolContacts")))
            {
                schoolContacts = JsonConvert.DeserializeObject<List<SchoolContact>>(File.ReadAllText(GetJsonPath("SchoolContacts")))!;
            }
            if (File.Exists(GetJsonPath("ClassSharedFiles")))
            {
                classSharedFiles = JsonConvert.DeserializeObject<List<ClassSharedFile>>(File.ReadAllText(GetJsonPath("ClassSharedFiles")))!;
            }
            if (File.Exists(GetJsonPath("SchoolSharedFiles")))
            {
                schoolSharedFiles = JsonConvert.DeserializeObject<List<SchoolSharedFile>>(File.ReadAllText(GetJsonPath("SchoolSharedFiles")))!;
            }
            if (File.Exists(GetJsonPath("ClassResults")))
            {
                classResults = JsonConvert.DeserializeObject<List<ClassResult>>(File.ReadAllText(GetJsonPath("ClassResults")))!;
            }
            if (File.Exists(GetJsonPath("Account")))
            {
                account = JsonConvert.DeserializeObject<Account>(File.ReadAllText(GetJsonPath("Account")))!;
                return true;
            }
            return false;
        }

        private void SaveJson()
        {
            try
            {
                File.WriteAllText(GetJsonPath("Reports"), JsonConvert.SerializeObject(reports));
            }
            catch
            { }
            try
            {
                File.WriteAllText(GetJsonPath("Quizzes"), JsonConvert.SerializeObject(quizzes));
            }
            catch
            { }
            try
            {
                File.WriteAllText(GetJsonPath("ClassContacts"), JsonConvert.SerializeObject(classContacts));
            }
            catch
            { }
            try
            {
                File.WriteAllText(GetJsonPath("SchoolContacts"), JsonConvert.SerializeObject(schoolContacts));
            }
            catch
            { }
            try
            {
                File.WriteAllText(GetJsonPath("ClassSharedFiles"), JsonConvert.SerializeObject(classSharedFiles));
            }
            catch
            { }
            try
            {
                File.WriteAllText(GetJsonPath("SchoolSharedFiles"), JsonConvert.SerializeObject(schoolSharedFiles));
            }
            catch
            { }
            try
            {
                File.WriteAllText(GetJsonPath("ClassResults"), JsonConvert.SerializeObject(classResults));
            }
            catch
            { }
            try
            {
                File.WriteAllText(GetJsonPath("Account"), JsonConvert.SerializeObject(account));
            }
            catch
            { }
        }

        public bool Login()
        {
            cookieContainer = new CookieContainer();
            httpClientHandler = new HttpClientHandler
            {
                AutomaticDecompression = ~DecompressionMethods.None,
                CookieContainer = cookieContainer
            };
            httpClient = new HttpClient(httpClientHandler);
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://gakujo.shizuoka.ac.jp/portal/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/login/preLogin/preLogin");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("mistakeChecker=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/shibbolethlogin/shibbolethLogin/initLogin/sso");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("selectLocale=ja&mistakeChecker=0&EXCLUDE_SET=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://idp.shizuoka.ac.jp/idp/profile/SAML2/Redirect/SSO?execution=e1s1");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("j_username=" + account.UserId + "&j_password=" + account.PassWord + "&_eventId_proceed=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            if (HttpUtility.HtmlDecode(httpResponse.Content.ReadAsStringAsync().Result).Contains("ユーザ名またはパスワードが正しくありません。"))
            {
                return false;
            }
            else
            {
                htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
                string relayState = htmlDocument.DocumentNode.SelectNodes("/html/body/form/div/input[1]")[0].Attributes["value"].Value;
                relayState = relayState.Replace("&#x3a;", ":");
                string SAMLResponse = htmlDocument.DocumentNode.SelectNodes("/html/body/form/div/input[2]")[0].Attributes["value"].Value;
                relayState = Uri.EscapeDataString(relayState);
                SAMLResponse = Uri.EscapeDataString(SAMLResponse);
                httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/Shibboleth.sso/SAML2/POST");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpRequestMessage.Content = new StringContent("RelayState=" + relayState + "&SAMLResponse=" + SAMLResponse);
                httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
                httpRequestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://gakujo.shizuoka.ac.jp/portal/shibbolethlogin/shibbolethLogin/initLogin/sso");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
                httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/home/home/initialize");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpRequestMessage.Content = new StringContent("EXCLUDE_SET=");
                httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
                htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
                account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
                account.StudentName = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/div/div/div/ul[2]/li/a/span/span")[0].InnerText;
                account.StudentName = account.StudentName[0..^2];
            }
            SaveJson();
            SaveCookies();
            return true;
        }

        public void GetReports(out int diffCount)
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&headTitle=授業サポート&menuCode=A02&nextPath=/report/student/searchList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            diffCount = reports.Count;
            reports.Clear();
            int limitCount = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr").Count;
            for (int i = 0; i < limitCount; i++)
            {
                Report report = new();
                report.Subjects = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[0].InnerText.Replace("\r", "").Replace("\n", "").Trim();
                report.Subjects = Regex.Replace(report.Subjects, @"\s+", " ");
                report.Title = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").InnerText.Trim();
                report.ReportId = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("'", "").Trim();
                report.SchoolYear = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[3].Replace("'", "").Trim();
                report.SubjectCode = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[4].Replace("'", "").Trim();
                report.ClassCode = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[5].Replace("'", "").Replace(");", "").Trim();
                report.Status = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].InnerText.Trim();
                report.StartDateTime = DateTime.Parse(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText.Trim().Split('～')[0]);
                report.EndDateTime = DateTime.Parse(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText.Trim().Split('～')[1]);
                if (htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[4].InnerText.Trim() != "")
                {
                    report.SubmittedDateTime = DateTime.Parse(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[4].InnerText.Trim());
                }
                report.ImplementationFormat = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[5].InnerText.Trim();
                report.Operation = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[6].InnerText.Trim();
                reports.Add(report);
            }
            diffCount = reports.Count - diffCount;
            SaveJson();
            SaveCookies();
        }

        public void GetQuizzes(out int diffCount)
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&headTitle=小テスト一覧&menuCode=A03&nextPath=/test/student/searchList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            diffCount = quizzes.Count;
            quizzes.Clear();
            int limitCount = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr").Count;
            for (int i = 0; i < limitCount; i++)
            {
                Quiz quiz = new();
                quiz.Subjects = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[0].InnerText.Replace("\r", "").Replace("\n", "").Trim();
                quiz.Subjects = Regex.Replace(quiz.Subjects, @"\s+", " ");
                quiz.Title = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").InnerText.Trim();
                quiz.QuizId = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("'", "").Trim();
                quiz.SchoolYear = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[3].Replace("'", "").Trim();
                quiz.SubjectCode = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[4].Replace("'", "").Trim();
                quiz.ClassCode = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[5].Replace("'", "").Replace(");", "").Trim();
                quiz.Status = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].InnerText.Trim();
                quiz.StartDateTime = DateTime.Parse(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText.Trim().Split('～')[0]);
                quiz.EndDateTime = DateTime.Parse(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText.Trim().Split('～')[1]);
                quiz.SubmissionStatus = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[4].InnerText.Trim();
                quiz.ImplementationFormat = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[5].InnerText.Trim();
                quiz.Operation = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[6].InnerText.Trim();
                quizzes.Add(quiz);
            }
            diffCount = quizzes.Count - diffCount;
            SaveJson();
            SaveCookies();
        }

        public void GetClassContacts(out int diffCount)
        {
            ClassContact? lastClassContact = classContacts.Count > 0 ? classContacts[0] : null;
            List<ClassContact> diffClassContacts = new() { };
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&headTitle=授業連絡一覧&menuCode=A01&nextPath=/classcontact/classContactList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            int limitCount = htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr").Count;
            for (int i = 0; i < limitCount; i++)
            {
                ClassContact classContact = new();
                classContact.Subjects = htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].InnerText.Replace("\r", "").Replace("\n", "").Trim();
                classContact.Subjects = Regex.Replace(classContact.Subjects, @"\s+", " ");
                classContact.TeacherName = htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].InnerText.Trim();
                classContact.Title = HttpUtility.HtmlDecode(htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].SelectSingleNode("a").InnerText).Trim();
                if (htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[5].InnerText.Trim() != "")
                {
                    classContact.TargetDateTime = DateTime.Parse(htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[5].InnerText.Trim());
                }
                classContact.ContactDateTime = DateTime.Parse(htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[6].InnerText.Trim());
                if (classContact.Equals(lastClassContact))
                {
                    break;
                }
                diffClassContacts.Add(classContact);
            }
            diffCount = diffClassContacts.Count;
            for (int i = 0; i < diffCount; i++)
            {
                classContacts.Insert(i, diffClassContacts[i]);
            }
            for (int i = 0; i < diffCount; i++)
            {
                GetClassContact(i);
            }
            SaveJson();
            SaveCookies();
        }

        public void GetClassContact(int indexCount)
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&headTitle=授業連絡一覧&menuCode=A01&nextPath=/classcontact/classContactList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/goDetail/" + indexCount);
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            string content = "org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&teacherCode=&schoolYear=" + schoolYear + "&semesterCode=" + semesterCode + "&subjectDispCode=&searchKeyWord=&checkSearchKeywordTeacherUserName=on&checkSearchKeywordSubjectName=on&checkSearchKeywordTitle=on&contactKindCode=&targetDateStart=&targetDateEnd=&reportDateStart=" + schoolYear + "/01/01&reportDateEnd=&requireResponse=&studentCode=&studentName=&tbl_A01_01_length=-1&_searchConditionDisp.accordionSearchCondition=false&_screenIdentifier=SC_A01_01&_screenInfoDisp=true&_scrollTop=0";
            httpRequestMessage.Content = new StringContent(content);
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            classContacts[indexCount].ContactType = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div/div/form/div[3]/div/div/div/table")[0].SelectNodes("tr")[0].SelectSingleNode("td").InnerText;
            classContacts[indexCount].Content = HttpUtility.HtmlDecode(htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div/div/form/div[3]/div/div/div/table")[0].SelectNodes("tr")[2].SelectSingleNode("td").InnerText);
            classContacts[indexCount].Content = Regex.Replace(classContacts[indexCount].Content, "[\\r\\n]+", Environment.NewLine, RegexOptions.Multiline);
            classContacts[indexCount].FileLinkRelease = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div/div/form/div[3]/div/div/div/table")[0].SelectNodes("tr")[4].SelectSingleNode("td").InnerText.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            classContacts[indexCount].ReferenceURL = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div/div/form/div[3]/div/div/div/table")[0].SelectNodes("tr")[5].SelectSingleNode("td").InnerText.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            classContacts[indexCount].Severity = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div/div/form/div[3]/div/div/div/table")[0].SelectNodes("tr")[6].SelectSingleNode("td").InnerText.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            classContacts[indexCount].WebReplyRequest = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div/div/form/div[3]/div/div/div/table")[0].SelectNodes("tr")[8].SelectSingleNode("td").InnerText;
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div/div/form/div[3]/div/div/div/table")[0].SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div") != null)
            {
                classContacts[indexCount].Files = new string[htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div/div/form/div[3]/div/div/div/table")[0].SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div/div/form/div[3]/div/div/div/table")[0].SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div").Count; i++)
                {
                    HtmlAgilityPack.HtmlNode htmlNode = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div/div/form/div[3]/div/div/div/table")[0].SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div")[i];
                    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[0].Replace("fileDownLoad('", "").Replace("'", "") + "&no=" + htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("');", "").Replace("'", "").Trim() + "&EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&prefix=default&sequence=&webspaceTabDisplayFlag=&screenName=&fileNameAutonumberFlag=&fileNameDisplayFlag=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
                    Stream stream = httpResponse.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath))
                    {
                        Directory.CreateDirectory(downloadPath);
                    }
                    using (FileStream fileStream = File.Create(Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    classContacts[indexCount].Files[i] = Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim());
                }
            }
            SaveJson();
            SaveCookies();
        }

        public void GetSchoolContacts(out int diffCount)
        {
            SchoolContact? lastSchoolContact = (schoolContacts.Count > 0) ? schoolContacts[0] : null;
            List<SchoolContact> diffSchoolContacts = new() { };
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&headTitle=キャンパスライフ&menuCode=B01&nextPath=/commoncontact/commonContact/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            for (int i = 0; i < htmlDocument.GetElementbyId("tbl_commoncontact_rcv").SelectSingleNode("tbody").SelectNodes("tr").Count; i++)
            {
                SchoolContact schoolContact = new();
                schoolContact.Category = htmlDocument.GetElementbyId("tbl_commoncontact_rcv").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[0].InnerText;
                schoolContact.Title = HttpUtility.HtmlDecode(htmlDocument.GetElementbyId("tbl_commoncontact_rcv").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").InnerText).Trim();
                schoolContact.ContactSource = htmlDocument.GetElementbyId("tbl_commoncontact_rcv").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].InnerText;
                schoolContact.ContactDateTime = DateTime.Parse(htmlDocument.GetElementbyId("tbl_commoncontact_rcv").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText.Trim());
                schoolContact.SchoolContactId = htmlDocument.GetElementbyId("tbl_commoncontact_rcv").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[0].Replace("dtlScreen('", "").Replace("'", "");
                if (schoolContact.Equals(lastSchoolContact))
                {
                    break;
                }
                diffSchoolContacts.Add(schoolContact);
            }
            diffCount = diffSchoolContacts.Count;
            for (int i = 0; i < diffCount; i++)
            {
                schoolContacts.Insert(i, diffSchoolContacts[i]);
            }
            for (int i = 0; i < diffCount; i++)
            {
                GetSchoolContact(i);
            }
            SaveJson();
            SaveCookies();
        }

        public void GetSchoolContact(int indexCount)
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&headTitle=キャンパスライフ&menuCode=B01&nextPath=/commoncontact/commonContact/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/commoncontact/commonContact/dtlEdit");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            string content = "org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&confirmMsg=&mode=initializeRcv&selectIndex=" + indexCount + "&commonContactId=" + schoolContacts[indexCount].SchoolContactId + "&commonContactKindDivision=1&_searchConditionDisp.tbl_commoncontact_rcv_dl=false&_screenIdentifier=SC_B01_01&_screenInfoDisp=true&_scrollTop=0";
            httpRequestMessage.Content = new StringContent(content);
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            schoolContacts[indexCount].Content = HttpUtility.HtmlDecode(htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form/div[3]/div/div/table")[0].SelectNodes("tr")[2].SelectSingleNode("td").InnerText);
            schoolContacts[indexCount].ContactSource = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form/div[3]/div/div/table")[0].SelectNodes("tr")[3].SelectSingleNode("td").InnerText;
            schoolContacts[indexCount].FileLinkRelease = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form/div[3]/div/div/table")[0].SelectNodes("tr")[5].SelectSingleNode("td").InnerText.Replace("\r", "").Replace("\r", "").Replace("\n", "").Replace("\t", "");
            schoolContacts[indexCount].ReferenceURL = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form/div[3]/div/div/table")[0].SelectNodes("tr")[6].SelectSingleNode("td").InnerText.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            schoolContacts[indexCount].Severity = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form/div[3]/div/div/table")[0].SelectNodes("tr")[7].SelectSingleNode("td").InnerText.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            schoolContacts[indexCount].WebReplyRequest = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form/div[3]/div/div/table")[0].SelectNodes("tr")[9].SelectSingleNode("td").InnerText;
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form/div[3]/div/div/table")[0].SelectNodes("tr")[4].SelectSingleNode("td/div") != null)
            {
                schoolContacts[indexCount].Files = new string[htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form/div[3]/div/div/table")[0].SelectNodes("tr")[4].SelectSingleNode("td/div").SelectNodes("div").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form/div[3]/div/div/table")[0].SelectNodes("tr")[4].SelectSingleNode("td/div").SelectNodes("div").Count; i++)
                {
                    HtmlAgilityPack.HtmlNode htmlNode = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form/div[3]/div/div/table")[0].SelectNodes("tr")[4].SelectSingleNode("td/div").SelectNodes("div")[i];
                    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[0].Replace("fileDownLoad('", "").Replace("'", "") + "&no=" + htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("');", "").Replace("'", "").Trim() + "&EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&prefix=default&sequence=&webspaceTabDisplayFlag=&screenName=&fileNameAutonumberFlag=&fileNameDisplayFlag=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
                    Stream stream = httpResponse.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath))
                    {
                        Directory.CreateDirectory(downloadPath);
                    }
                    using (FileStream fileStream = File.Create(Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    schoolContacts[indexCount].Files[i] = Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim());
                }
            }
            SaveJson();
            SaveCookies();
        }

        public void GetClassSharedFiles(out int diffCount)
        {
            ClassSharedFile? lastClassSharedFile = (classSharedFiles.Count > 0) ? classSharedFiles[0] : null;
            List<ClassSharedFile> diffClassSharedFiles = new() { };
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&headTitle=授業共有ファイル&menuCode=A08&nextPath=/classfile/classFile/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            int limitCount = htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr").Count;
            for (int i = 0; i < limitCount; i++)
            {
                ClassSharedFile classSharedFile = new();
                classSharedFile.Subjects = htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].InnerText.Replace("\r", "").Replace("\n", "").Trim();
                classSharedFile.Subjects = Regex.Replace(classSharedFile.Subjects, @"\s+", " ");
                classSharedFile.Title = HttpUtility.HtmlDecode(htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].SelectSingleNode("a").InnerText).Trim();
                classSharedFile.Size = htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText;
                classSharedFile.UpdateDateTime = DateTime.Parse(htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[4].InnerText);
                if (classSharedFile.Equals(lastClassSharedFile))
                {
                    break;
                }
                diffClassSharedFiles.Add(classSharedFile);
            }
            diffCount = diffClassSharedFiles.Count;
            for (int i = 0; i < diffCount; i++)
            {
                classSharedFiles.Insert(i, diffClassSharedFiles[i]);
            }
            for (int i = 0; i < diffCount; i++)
            {
                GetClassSharedFile(i);
            }
            SaveJson();
            SaveCookies();
        }

        public void GetClassSharedFile(int indexCount)
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&headTitle=授業共有ファイル&menuCode=A08&nextPath=/classfile/classFile/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/showClassFileDetail?EXCLUDE_SET=&org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&selectIndex=" + indexCount +
"&_screenIdentifier=SC_A08_01&_screenInfoDisp=true&_searchConditionDisp.accordionSearchCondition=false&_scrollTop=0");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Headers.TryAddWithoutValidation("Referer", "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            classSharedFiles[indexCount].Description = HttpUtility.HtmlDecode(htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]")[0].SelectNodes("tr")[2].SelectSingleNode("td").InnerText);
            classSharedFiles[indexCount].PublicPeriod = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]")[0].SelectNodes("tr")[3].SelectSingleNode("td").InnerText.Replace("\n", "").Replace("\t", "").Replace("&nbsp;", "");
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]")[0].SelectNodes("tr")[1].SelectSingleNode("td/div") != null)
            {
                classSharedFiles[indexCount].Files = new string[htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]")[0].SelectNodes("tr")[1].SelectSingleNode("td/div").SelectNodes("div").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]")[0].SelectNodes("tr")[1].SelectSingleNode("td/div").SelectNodes("div").Count; i++)
                {
                    HtmlAgilityPack.HtmlNode htmlNode = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]")[0].SelectNodes("tr")[1].SelectSingleNode("td/div").SelectNodes("div")[i];
                    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[0].Replace("fileDownLoad('", "").Replace("'", "") + "&no=" + htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("');", "").Replace("'", "").Trim() + "&EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&prefix=default&sequence=&webspaceTabDisplayFlag=&screenName=&fileNameAutonumberFlag=&fileNameDisplayFlag=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
                    Stream stream = httpResponse.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath))
                    {
                        Directory.CreateDirectory(downloadPath);
                    }
                    using (FileStream fileStream = File.Create(Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    classSharedFiles[indexCount].Files[i] = Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim());
                }
            }
            SaveJson();
            SaveCookies();
        }

        public void GetSchoolSharedFiles(out int diffCount)
        {
            SchoolSharedFile? lastSchoolSharedFile = (schoolSharedFiles.Count > 0) ? schoolSharedFiles[0] : null;
            List<SchoolSharedFile> diffSchoolSharedFiles = new() { };
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&headTitle=学内共有ファイル&menuCode=B08&nextPath=/commonfile/commonFile/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            int limitCount = 0;
            if (htmlDocument.GetElementbyId("tbl_commonFileList") != null)
            {
                limitCount = htmlDocument.GetElementbyId("tbl_commonFileList").SelectSingleNode("tbody").SelectNodes("tr").Count;
            }
            for (int i = 0; i < limitCount; i++)
            {
                SchoolSharedFile schoolSharedFile = new();
                schoolSharedFile.Category = htmlDocument.GetElementbyId("tbl_commonFileList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].InnerText.Replace("\r", "").Replace("\n", "").Trim();
                schoolSharedFile.Title = HttpUtility.HtmlDecode(htmlDocument.GetElementbyId("tbl_commonFileList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].SelectSingleNode("a").InnerText).Trim();
                schoolSharedFile.DownloadCount = int.Parse(htmlDocument.GetElementbyId("tbl_commonFileList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText);
                schoolSharedFile.Size = htmlDocument.GetElementbyId("tbl_commonFileList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[4].InnerText;
                schoolSharedFile.UpdateDateTime = DateTime.Parse(htmlDocument.GetElementbyId("tbl_commonFileList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[5].InnerText);
                if (schoolSharedFile.Equals(lastSchoolSharedFile))
                {
                    break;
                }
                schoolSharedFile.SchoolSharedFileId = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/input[" + (i + 1) + "]").Attributes["value"].Value;
                diffSchoolSharedFiles.Add(schoolSharedFile);
            }
            diffCount = diffSchoolSharedFiles.Count;
            for (int i = 0; i < diffCount; i++)
            {
                schoolSharedFiles.Insert(i, diffSchoolSharedFiles[i]);
            }
            for (int i = 0; i < diffCount; i++)
            {
                GetSchoolSharedFile(i);
            }
            SaveJson();
            SaveCookies();
        }

        public void GetSchoolSharedFile(int indexCount)
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&headTitle=学内共有ファイル&menuCode=B08&nextPath=/commonfile/commonFile/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/commonfile/commonFileDetail/initialize");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&searchKeyWord=&searchTitleName=Y&commonFileCategoryId=&lastUpdateDate=&tbl_commonFileList_length=50&prevPageId=backToList&hiddenDeleteCommonFileId=&hiddenCommonFileId=" + schoolSharedFiles[indexCount].SchoolSharedFileId + "&confirmMsg=&tableName=T_GROUP_MANAGE&backPath=/commonfile/commonFile/initialize&_searchConditionDisp.accordionSearchCondition=false&_screenIdentifier=SC_B08_01&_screenInfoDisp=true&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div[1]/form[1]/div/input")[0].Attributes["value"].Value;
            schoolSharedFiles[indexCount].Description = HttpUtility.HtmlDecode(htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/table")[0].SelectNodes("tr")[3].SelectSingleNode("td").InnerText);
            schoolSharedFiles[indexCount].PublicPeriod = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/table")[0].SelectNodes("tr")[4].SelectSingleNode("td").InnerText.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("&nbsp;", "");
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/table")[0].SelectNodes("tr")[2].SelectSingleNode("td/div") != null)
            {
                schoolSharedFiles[indexCount].Files = new string[htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/table")[0].SelectNodes("tr")[2].SelectSingleNode("td/div").SelectNodes("div").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/table")[0].SelectNodes("tr")[2].SelectSingleNode("td/div").SelectNodes("div").Count; i++)
                {
                    HtmlAgilityPack.HtmlNode htmlNode = htmlDocument.DocumentNode.SelectNodes("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/table")[0].SelectNodes("tr")[2].SelectSingleNode("td/div").SelectNodes("div")[i];
                    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[0].Replace("fileDownLoad('", "").Replace("'", "") + "&no=" + htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("');", "").Replace("'", "").Trim() + "&EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&prefix=default&sequence=&webspaceTabDisplayFlag=&screenName=&fileNameAutonumberFlag=&fileNameDisplayFlag=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
                    Stream stream = httpResponse.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath))
                    {
                        Directory.CreateDirectory(downloadPath);
                    }
                    using (FileStream fileStream = File.Create(Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    schoolSharedFiles[indexCount].Files[i] = Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim());
                }
            }
            SaveJson();
            SaveCookies();
        }

        //public string GetQuiz(Quiz quiz)
        //{
        //    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/forwardSubmitRef");
        //    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        //    httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&testId=" + quiz.QuizId + "&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=" + quiz.SchoolYear + "&listSubjectCode=" + quiz.SubjectCode + "&listClassCode=" + quiz.ClassCode + "&schoolYear=" + schoolYear + "&semesterCode=" + semesterCode + "&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=10&_searchConditionDisp.accordionSearchCondition=false&_screenIdentifier=SC_A03_01_G&_screenInfoDisp=&_scrollTop=0");
        //    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        //    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
        //    htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
        //    account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/form[1]/div/input")[0].Attributes["value"].Value;
        //    return htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form").InnerHtml;
        //}

        //public bool SubmitQuiz(string testId, string outputText)
        //{
        //    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/test/student/submit/confirmAction");
        //    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        //    httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&studentCode=" + account.ApacheToken + "&testId=" + testId + "&questionNumber=&selectedKey=&backPath=/test/student/searchList/selfForward" + Uri.EscapeUriString(outputText) + "&maxFileSize=5&_screenIdentifier=SC_A03_02_G&_screenInfoDisp=&_scrollTop=0");
        //    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        //    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
        //    htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
        //    account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/form[1]/div/input")[0].Attributes["value"].Value;
        //    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/test/student/submitConfirm/confirmAction");
        //    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        //    httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&studentCode=" + account.StudentCode + "&testId=" + testId + "&questionNumber=/test/student/submit/selfForward&maxFileSize=5&_screenIdentifier=SC_A03_03_G&_screenInfoDisp=&_scrollTop=0");
        //    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        //    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
        //    htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
        //    Login();
        //    return true;
        //}

        //public bool CancelQuiz(string testId)
        //{
        //    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/submitCancel");
        //    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        //    httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&testId=" + testId + "&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=/test/student/submit/selfForward&schoolYear=" + schoolYear + "&semesterCode=" + semesterCode + "&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=10&_searchConditionDisp.accordionSearchCondition=false&_screenIdentifier=SC_A03_01_G&_screenInfoDisp=&_scrollTop=0");
        //    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        //    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
        //    htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
        //    account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/form[1]/div/input")[0].Attributes["value"].Value;
        //    return true;
        //}

        //public string GetReport(Report report)
        //{
        //    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/forwardSubmitRef?submitStatusCode=01");
        //    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        //    httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&reportId=" + report.ReportId + "&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=" + report.SchoolYear + "&listSubjectCode=" + report.SubjectCode + "&listClassCode=" + report.ClassCode + "&schoolYear=" + schoolYear + "&semesterCode=" + semesterCode + "&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=10&_searchConditionDisp.accordionSearchCondition=false&_screenIdentifier=SC_A02_01_G&_screenInfoDisp=&_scrollTop=0");
        //    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        //    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
        //    htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
        //    account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/form[1]/div/input")[0].Attributes["value"].Value;
        //    htmlDocument.GetElementbyId("accordionInformation").Remove();
        //    return htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form").InnerHtml;
        //}

        //public bool SubmitReport(string reportId, string[] fileArray, string comment)
        //{
        //    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/forwardSubmit");
        //    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        //    httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&reportId=" + reportId + "&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&schoolYear=" + schoolYear + "&semesterCode=" + semesterCode + "&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=10&_searchConditionDisp.accordionSearchCondition=false&_screenIdentifier=SC_A02_01_G&_screenInfoDisp=&_scrollTop=0");
        //    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        //    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
        //    htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
        //    account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/form[1]/div/input")[0].Attributes["value"].Value;
        //    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/fileUpload/fileUploadInit");
        //    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        //    httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&prefix=reportFile&sequence=true&webspaceTabDisplayFlag=true&screenName=ファイル添付&fileNameAutonumberFlag=true&fileNameDisplayFlag=true");
        //    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        //    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
        //    htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
        //    account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/div/div/div/div[3]/div/div/div[1]/div[2]/form/div[1]/input")[0].Attributes["value"].Value;
        //    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/fileUploadLocal/fileUploadLocal");
        //    httpRequestMessage.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        //    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        //    MultipartFormDataContent multipartFormDataContent = new MultipartFormDataContent();
        //    multipartFormDataContent.Add(new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken));
        //    using (FileStream fileStream = new FileStream(fileArray[0], FileMode.Open, FileAccess.Read))
        //    {
        //        StreamContent streamContent = new StreamContent(fileStream);
        //        streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        //        {
        //            Name = "formFile",
        //            FileName = Path.GetFileName(fileArray[0])
        //        };
        //        streamContent.Headers.ContentType = new MediaTypeHeaderValue(MimeMapping.MimeUtility.GetMimeMapping(Path.GetFileName(fileArray[0])));
        //        multipartFormDataContent.Add(streamContent);
        //        httpRequestMessage.Content = multipartFormDataContent;
        //        httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
        //    }
        //    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/report/student/reportEntry/regist");
        //    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        //    httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&studentName=" + account.StudentName + "&studentCode=" + account.StudentCode + "&submitNo=&fileNo=&backPath=/report/student/searchList/selfForward&submitFileHidden=&maxFileSize=10&comment=" + Uri.EscapeUriString(comment) + "&_screenIdentifier=SC_A02_02_G&_screenInfoDisp=&_scrollTop=0");
        //    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        //    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
        //    Login();
        //    return true;
        //}

        //public bool CancelReport(Report report)
        //{
        //    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/submitCancel");
        //    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        //    httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&reportId=" + report.ReportId + "&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=" + report.SchoolYear + "&listSubjectCode=" + report.SubjectCode + "&listClassCode=" + report.ClassCode + "&schoolYear=" + schoolYear + "&semesterCode=" + semesterCode + "&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=10&_searchConditionDisp.accordionSearchCondition=false&_screenIdentifier=SC_A02_01_G&_screenInfoDisp=&_scrollTop=0");
        //    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
        //    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
        //    htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
        //    account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/form[1]/div/input")[0].Attributes["value"].Value;
        //    return true;
        //}

        private bool CheckConnection()
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("org.apache.struts.taglib.html.TOKEN=" + account.ApacheToken + "&headTitle=ホーム&menuCode=Z07&nextPath=/home/home/initialize&_screenIdentifier=&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/form[1]/div/input") == null)
            {
                cookieContainer = new CookieContainer();
                httpClientHandler = new HttpClientHandler
                {
                    AutomaticDecompression = ~DecompressionMethods.None,
                    CookieContainer = cookieContainer
                };
                httpClient = new HttpClient(httpClientHandler);
                return false;
            }
            account.ApacheToken = htmlDocument.DocumentNode.SelectNodes("/html/body/form[1]/div/input")[0].Attributes["value"].Value;
            SaveJson();
            SaveCookies();
            return true;
        }

        private bool SetAcademicSystem()
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/preLogin.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/sso/loginStudent.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("loginID=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            SaveJson();
            SaveCookies();
            return true;
        }

        public void GetClassResults()
        {
            SetAcademicSystem();
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/seisekiSearchStudentInit.do?mainMenuCode=008&parentMenuCode=007");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/table[5]/tr/td/table") == null)
            {
                return;
            }
            classResults.Clear();
            for (int i = 1; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[5]/tr/td/table").SelectNodes("tr").Count; i++)
            {
                HtmlAgilityPack.HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[5]/tr/td/table").SelectNodes("tr")[i];
                ClassResult classResult = new();
                classResult.Subjects = htmlNode.SelectNodes("td")[0].InnerText.Trim();
                classResult.TeacherName = htmlNode.SelectNodes("td")[1].InnerText.Trim();
                classResult.SubjectsSection = htmlNode.SelectNodes("td")[2].InnerText.Trim();
                classResult.SelectionSection = htmlNode.SelectNodes("td")[3].InnerText.Trim();
                classResult.SchoolCredit = int.Parse(htmlNode.SelectNodes("td")[4].InnerText.Trim());
                classResult.Evaluation = htmlNode.SelectNodes("td")[5].InnerText.Trim();
                if (htmlNode.SelectNodes("td")[6].InnerText.Trim() != "")
                {
                    classResult.Score = double.Parse(htmlNode.SelectNodes("td")[6].InnerText.Trim());
                }
                if (htmlNode.SelectNodes("td")[7].InnerText.Trim() != "")
                {
                    classResult.GP = double.Parse(htmlNode.SelectNodes("td")[7].InnerText.Trim());
                }
                classResult.AcquisitionYear = htmlNode.SelectNodes("td")[8].InnerText.Trim();
                classResult.ReportDate = DateTime.Parse(htmlNode.SelectNodes("td")[9].InnerText.Trim());
                classResults.Add(classResult);
            }
            SaveJson();
            SaveCookies();
        }
    }


    public class Account
    {
        public string UserId = "";
        public string PassWord = "";
        public string StudentName = "";
        public string StudentCode = "";
        public string ApacheToken = "";
    }

    // : IComparable
    public class Report
    {
        public string Subjects { get; set; } = "";
        public string Title { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public DateTime SubmittedDateTime { get; set; }
        public string ImplementationFormat { get; set; } = "";
        public string Operation { get; set; } = "";
        public string ReportId { get; set; } = "";
        public string SchoolYear { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string ClassCode { get; set; } = "";

        public override string ToString()
        {
            return "[" + Status + "] " + Subjects.Split(' ')[0] + " " + Title + " -> " + EndDateTime.ToString();
        }

        public string ToShortString()
        {
            return Subjects.Split(' ')[0] + " " + Title;
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            Report objReport = (Report)obj;
            return SubjectCode == objReport.SubjectCode && ClassCode == objReport.ClassCode && ReportId == objReport.ReportId;
        }

        public override int GetHashCode()
        {
            return SubjectCode.GetHashCode() ^ ClassCode.GetHashCode() ^ ReportId.GetHashCode();
        }

        //public int CompareTo(Object obj)
        //{
        //    if (obj == null || GetType() != obj.GetType())
        //    {
        //        return 1;
        //    }
        //    Report objReport = (Report)obj;
        //    if (EndDateTime > objReport.EndDateTime)
        //    {
        //        return 1;
        //    }
        //    else
        //    {
        //        if (Status == "未提出")
        //        {
        //            return 1;
        //        }
        //        if (Status == "提出済")
        //        {
        //            return -1;
        //        }
        //        return SubjectCode.CompareTo(objReport.SubjectCode);
        //    }
        //}
    }

    public class Quiz
    {
        public string Subjects { get; set; } = "";
        public string Title { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string SubmissionStatus { get; set; } = "";
        public string ImplementationFormat { get; set; } = "";
        public string Operation { get; set; } = "";
        public string QuizId { get; set; } = "";
        public string SchoolYear { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string ClassCode { get; set; } = "";

        public override string ToString()
        {
            return "[" + SubmissionStatus + "] " + Subjects.Split(' ')[0] + " " + Title + " -> " + EndDateTime.ToString();
        }

        public string ToShortString()
        {
            return Subjects.Split(' ')[0] + " " + Title;
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            Quiz objQuiz = (Quiz)obj;
            return SubjectCode == objQuiz.SubjectCode && ClassCode == objQuiz.ClassCode && QuizId == objQuiz.QuizId;
        }

        public override int GetHashCode()
        {
            return SubjectCode.GetHashCode() ^ ClassCode.GetHashCode() ^ QuizId.GetHashCode();
        }

        //public int CompareTo(Object obj)
        //{
        //    if (obj == null || GetType() != obj.GetType())
        //    {
        //        return 1;
        //    }
        //    Quiz objQuiz = (Quiz)obj;
        //    if (EndDateTime > objQuiz.EndDateTime)
        //    {
        //        return 1;
        //    }
        //    else
        //    {
        //        if (Status == "受付中")
        //        {
        //            return 1;
        //        }
        //        if (Status == "提出済")
        //        {
        //            return -1;
        //        }
        //        return SubjectCode.CompareTo(objQuiz.SubjectCode);
        //    }
        //}
    }

    public class ClassContact
    {
        public string Subjects { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string ContactType { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string[] Files { get; set; } = Array.Empty<string>();
        public string FileLinkRelease { get; set; } = "";
        public string ReferenceURL { get; set; } = "";
        public string Severity { get; set; } = "";
        public DateTime TargetDateTime { get; set; }
        public DateTime ContactDateTime { get; set; }
        public string WebReplyRequest { get; set; } = "";

        public override string ToString()
        {
            return Subjects.Split(' ')[0] + " " + Title + " " + ContactDateTime.ToShortDateString();
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            ClassContact objClassContact = (ClassContact)obj;
            return Subjects == objClassContact.Subjects && Title == objClassContact.Title && ContactDateTime == objClassContact.ContactDateTime;
        }

        public override int GetHashCode()
        {
            return Subjects.GetHashCode() ^ Title.GetHashCode() ^ ContactDateTime.GetHashCode();
        }
    }

    public class SchoolContact
    {
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string ContactSource { get; set; } = "";
        public string[] Files { get; set; } = Array.Empty<string>();
        public string FileLinkRelease { get; set; } = "";
        public string ReferenceURL { get; set; } = "";
        public string Severity { get; set; } = "";
        public DateTime ContactDateTime { get; set; }
        public string WebReplyRequest { get; set; } = "";
        public string ManagementAffiliation { get; set; } = "";
        public string SchoolContactId { get; set; } = "";

        public override string ToString()
        {
            return Category + " " + Title + " " + ContactDateTime.ToShortDateString();
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            SchoolContact objSchoolContact = (SchoolContact)obj;
            return ContactSource == objSchoolContact.ContactSource && Title == objSchoolContact.Title && ContactDateTime == objSchoolContact.ContactDateTime;
        }

        public override int GetHashCode()
        {
            return ContactSource.GetHashCode() ^ Title.GetHashCode() ^ ContactDateTime.GetHashCode();
        }
    }

    public class ClassSharedFile
    {
        public string Subjects { get; set; } = "";
        public string Title { get; set; } = "";
        public string Size { get; set; } = "";
        public string[] Files { get; set; } = Array.Empty<string>();
        public string Description { get; set; } = "";
        public string PublicPeriod { get; set; } = "";
        public DateTime UpdateDateTime { get; set; }

        public override string ToString()
        {
            return Subjects.Split(' ')[0] + " " + Title + " " + UpdateDateTime.ToShortDateString();
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            ClassSharedFile objClassSharedFile = (ClassSharedFile)obj;
            return Subjects == objClassSharedFile.Subjects && Title == objClassSharedFile.Title && UpdateDateTime == objClassSharedFile.UpdateDateTime;
        }

        public override int GetHashCode()
        {
            return Subjects.GetHashCode() ^ Title.GetHashCode() ^ UpdateDateTime.GetHashCode();
        }
    }

    public class SchoolSharedFile
    {
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public int DownloadCount { get; set; }
        public string Size { get; set; } = "";
        public string[] Files { get; set; } = Array.Empty<string>();
        public string Description { get; set; } = "";
        public string PublicPeriod { get; set; } = "";
        public DateTime UpdateDateTime { get; set; }
        public string SchoolSharedFileId { get; set; } = "";

        public override string ToString()
        {
            return Category + " " + Title + " " + UpdateDateTime.ToShortDateString();
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            SchoolSharedFile objSchoolSharedFile = (SchoolSharedFile)obj;
            return Category == objSchoolSharedFile.Category && Title == objSchoolSharedFile.Title && UpdateDateTime == objSchoolSharedFile.UpdateDateTime;
        }

        public override int GetHashCode()
        {
            return Category.GetHashCode() ^ Title.GetHashCode() ^ UpdateDateTime.GetHashCode();
        }
    }

    public class ClassResult
    {
        public string Subjects { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string SubjectsSection { get; set; } = "";
        public string SelectionSection { get; set; } = "";
        public int SchoolCredit { get; set; }
        public string Evaluation { get; set; } = "";
        public double Score { get; set; }
        public double GP { get; set; }
        public string AcquisitionYear { get; set; } = "";
        public DateTime ReportDate { get; set; }
        public string TestType { get; set; } = "";

        public override string ToString()
        {
            return Subjects + " " + Score + " (" + Evaluation + ") " + GP + " " + ReportDate.ToShortDateString();
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            ClassResult objClassResult = (ClassResult)obj;
            return Subjects == objClassResult.Subjects && AcquisitionYear == objClassResult.AcquisitionYear;
        }

        public override int GetHashCode()
        {
            return Subjects.GetHashCode() ^ AcquisitionYear.GetHashCode();
        }
    }
}