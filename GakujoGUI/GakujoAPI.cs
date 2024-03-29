﻿using GakujoGUI.ExceptionModel;
using GakujoGUI.Models;
using HtmlAgilityPack;
using Newtonsoft.Json;
using NLog;
using ReverseMarkdown;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;


namespace GakujoGUI
{
    internal class GakujoApi
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public Account Account { get; set; } = new();
        public List<Report> Reports { get; set; } = new();
        public List<Quiz> Quizzes { get; set; } = new();
        public List<ClassContact> ClassContacts { get; set; } = new();
        public List<ClassSharedFile> ClassSharedFiles { get; set; } = new();
        public List<List<LotteryRegistration>> LotteryRegistrations => ClassTables.Select(x => x.LotteryRegistrations).ToList();
        public List<List<LotteryRegistrationResult>> LotteryRegistrationsResult => ClassTables.Select(x => x.LotteryRegistrationsResult).ToList();
        public List<List<GeneralRegistration>> RegisterableGeneralRegistrations => ClassTables.Select(x => x.RegisterableGeneralRegistrations).ToList();
        public List<LotteryRegistrationEntry> LotteryRegistrationEntries { get; set; } = new();
        public List<GeneralRegistrationEntry> GeneralRegistrationEntries { get; set; } = new();
        public SchoolGrade SchoolGrade { get; set; } = new();
        public List<ClassTableRow> ClassTables { get; set; } = new();

        private CookieContainer cookieContainer = new();
        private HttpClientHandler httpClientHandler = new();
        private HttpClient httpClient = new();
        private HttpRequestMessage httpRequestMessage = new();
        private HttpResponseMessage httpResponseMessage = new();

        private readonly string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @$"{AssemblyName}\Download\");

        private readonly string schoolYear;
        private readonly int semesterCode;
        private readonly string userAgent;
        private string SchoolYearSemesterCodeSuffix => $"_{schoolYear}_{ReplaceSemesterCode(semesterCode)}";
        private DateTime ReportDateStart => new(int.Parse(schoolYear), semesterCode < 2 ? 3 : 9, 1);

        private DateTime ReportDateEnd
        {
            get
            {
                var dateTime = ReportDateStart.AddMonths(6);
                return new(dateTime.Year, dateTime.Month, DateTime.DaysInMonth(dateTime.Year, dateTime.Month));
            }
        }

        private static string GetJsonPath(string value)
        {
            if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AssemblyName)))
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AssemblyName));
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @$"{AssemblyName}\{value}.json");
        }

        private static readonly string AssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

        public static string Protect(string stringToEncrypt, string? optionalEntropy, DataProtectionScope scope) => Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(stringToEncrypt), optionalEntropy != null ? Encoding.UTF8.GetBytes(optionalEntropy) : null, scope));

        public static string Unprotect(string encryptedString, string? optionalEntropy, DataProtectionScope scope)
        {
            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encryptedString),
                    optionalEntropy != null ? Encoding.UTF8.GetBytes(optionalEntropy) : null, scope));
            }
            catch
            {
                return encryptedString;
            }
        }

        private static string ReplaceColon(string value) => value.Replace("&#x3a;", ":").Trim();

        private static string ReplaceSpace(string value) => Regex.Replace(Regex.Replace(value.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("&nbsp;", "").Trim(), @"\s+", " "), @" +", " ").Trim();

        private static string ReplaceJsArgs(string value, int index) => value.Split(',')[index].Replace("'", "").Replace("(", "").Replace(")", "").Replace(";", "").Trim();

        private static DateTime ReplaceTimeSpan(string value, int index) => ReplaceDateTime(value.Trim().Split('～')[index]);

        private static DateTime ReplaceDateTime(string value)
        {
            var replacedValue = Regex.Replace(value, @"24:(\d\d)$", "00:$1");
            var replacedDateTime = DateTime.Parse(replacedValue);
            return value == replacedValue ? replacedDateTime : replacedDateTime.AddDays(1);
        }

        public static string ReplaceHtmlMarkdown(string value)
        {
            Config config = new()
            {
                UnknownTags = Config.UnknownTagsOption.Bypass,
                GithubFlavored = true,
                RemoveComments = true,
                SmartHrefHandling = true,
            };
            value = new Converter(config).Convert(value);
            return Regex.Replace(Regex.Replace(value, @" +", " ").Replace("|\r\n\n \n |", "|\r\n|"), "(?<=[^|])\\r\\n(?=[^|])", "  \r\n");
        }

        private static string ReplaceHtmlNewLine(string value) => Regex.Replace(HttpUtility.HtmlDecode(value).Replace("<br>", " \r\n").Trim('\r').Trim('\n'), "[\\r\\n]+", Environment.NewLine, RegexOptions.Multiline).Trim();

        private static int ReplaceSemesterCode(int value) => value < 2 ? 1 : 2;

        private static int ReplaceWeekday(string value)
        {
            return value[..1] switch
            {
                "月" => 0,
                "火" => 1,
                "水" => 2,
                "木" => 3,
                "金" => 4,
                _ => -1,
            };
        }

        private static int ReplacePeriod(string value) => (int.Parse(value.Substring(1, 1)) + 1) / 2;

        private static string ReplaceWeekday(int index) => new[] { "月", "火", "水", "木", "金" }[index];

        private static string ReplacePeriod(int index) => new[] { "1･2", "3･4", "5･6", "7･8", "9･10", "11･12", "13･14" }[index];

        public static string ReplaceSubjectsShort(string value) => Regex.Replace(value, "（.*）(前|後)期.*", "");

        private void GetApacheToken(HtmlDocument htmlDocument, bool required = true)
        {
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input") == null && required)
                throw new TokenNotFoundException();
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
        }


        public GakujoApi(string schoolYear, int semesterCode, string userAgent)
        {
            this.schoolYear = schoolYear;
            this.semesterCode = semesterCode;
            this.userAgent = userAgent;
            Logger.Info($"Initialize GakujoAPI schoolYear={schoolYear}, semesterCode={semesterCode}, userAgent={userAgent}.");
            LoadConfigs();
        }

        public void SetAccount(string userId, string passWord)
        {
            Account.UserId = Protect(userId, null!, DataProtectionScope.CurrentUser);
            Account.PassWord = Protect(passWord, null!, DataProtectionScope.CurrentUser);
            Logger.Info("Set Account.");
            SaveConfigs();
        }

        public bool LoadConfigs()
        {
            Logger.Info("Load Configs.");
            if (File.Exists(GetJsonPath("Reports" + SchoolYearSemesterCodeSuffix)))
                Reports = JsonConvert.DeserializeObject<List<Report>>(File.ReadAllText(GetJsonPath("Reports" + SchoolYearSemesterCodeSuffix)))! ?? new();
            if (File.Exists(GetJsonPath("Quizzes" + SchoolYearSemesterCodeSuffix)))
                Quizzes = JsonConvert.DeserializeObject<List<Quiz>>(File.ReadAllText(GetJsonPath("Quizzes" + SchoolYearSemesterCodeSuffix)))! ?? new();
            if (File.Exists(GetJsonPath("ClassContacts" + SchoolYearSemesterCodeSuffix)))
                ClassContacts = JsonConvert.DeserializeObject<List<ClassContact>>(File.ReadAllText(GetJsonPath("ClassContacts" + SchoolYearSemesterCodeSuffix)))! ?? new();
            if (File.Exists(GetJsonPath("ClassSharedFiles" + SchoolYearSemesterCodeSuffix)))
                ClassSharedFiles = JsonConvert.DeserializeObject<List<ClassSharedFile>>(File.ReadAllText(GetJsonPath("ClassSharedFiles" + SchoolYearSemesterCodeSuffix)))! ?? new();
            if (File.Exists(GetJsonPath("LotteryRegistrationEntries")))
                LotteryRegistrationEntries = JsonConvert.DeserializeObject<List<LotteryRegistrationEntry>>(File.ReadAllText(GetJsonPath("LotteryRegistrationEntries")))! ?? new();
            if (File.Exists(GetJsonPath("GeneralRegistrationEntries")))
                GeneralRegistrationEntries = JsonConvert.DeserializeObject<List<GeneralRegistrationEntry>>(File.ReadAllText(GetJsonPath("GeneralRegistrationEntries")))! ?? new();
            if (File.Exists(GetJsonPath("SchoolGrade")))
                SchoolGrade = JsonConvert.DeserializeObject<SchoolGrade>(File.ReadAllText(GetJsonPath("SchoolGrade")))!;
            if (File.Exists(GetJsonPath("ClassTables")))
                ClassTables = JsonConvert.DeserializeObject<List<ClassTableRow>>(File.ReadAllText(GetJsonPath("ClassTables")))! ?? new();
            ApplyReportsClassTables();
            ApplyQuizzesClassTables();
            if (!File.Exists(GetJsonPath("Account")))
                return false;
            Account = JsonConvert.DeserializeObject<Account>(File.ReadAllText(GetJsonPath("Account")))!;
            return true;
        }

        public void SaveConfigs()
        {
            Logger.Info("Save Configs.");
            File.WriteAllText(GetJsonPath("Reports" + SchoolYearSemesterCodeSuffix), JsonConvert.SerializeObject(Reports, Formatting.Indented));
            File.WriteAllText(GetJsonPath("Quizzes" + SchoolYearSemesterCodeSuffix), JsonConvert.SerializeObject(Quizzes, Formatting.Indented));
            File.WriteAllText(GetJsonPath("ClassContacts" + SchoolYearSemesterCodeSuffix), JsonConvert.SerializeObject(ClassContacts, Formatting.Indented));
            File.WriteAllText(GetJsonPath("ClassSharedFiles" + SchoolYearSemesterCodeSuffix), JsonConvert.SerializeObject(ClassSharedFiles, Formatting.Indented));
            File.WriteAllText(GetJsonPath("LotteryRegistrationEntries"), JsonConvert.SerializeObject(LotteryRegistrationEntries, Formatting.Indented));
            File.WriteAllText(GetJsonPath("GeneralRegistrationEntries"), JsonConvert.SerializeObject(GeneralRegistrationEntries, Formatting.Indented));
            File.WriteAllText(GetJsonPath("SchoolGrade"), JsonConvert.SerializeObject(SchoolGrade, Formatting.Indented));
            File.WriteAllText(GetJsonPath("ClassTables"), JsonConvert.SerializeObject(ClassTables, Formatting.Indented));
            File.WriteAllText(GetJsonPath("Account"), JsonConvert.SerializeObject(Account, Formatting.Indented));
        }

        public void Login()
        {
            Logger.Info("Start Login.");
            if (DateTime.Now.Hour is >= 3 and < 5)
            {
                Logger.Warn("Return Login by overtime.");
                throw new UnableConnectException();
            }
            try
            {
                httpRequestMessage = new(new("GET"), "http://clients3.google.com/generate_204");
                httpClient.SendAsync(httpRequestMessage).Wait();
            }
            catch
            {
                Logger.Warn("Return Login by not network available.");
                throw new UnableConnectException();
            }
            cookieContainer = new();
            if (Account.AccessEnvironmentKey != "" && Account.AccessEnvironmentValue != "")
                cookieContainer.Add(new Cookie(Account.AccessEnvironmentKey, Account.AccessEnvironmentValue) { Domain = "gakujo.shizuoka.ac.jp" });
            httpClientHandler = new() { AutomaticDecompression = ~DecompressionMethods.None, CookieContainer = cookieContainer };
            httpClient = new(httpClientHandler);
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/portal/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/portal/");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/login/preLogin/preLogin");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("mistakeChecker=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/login/preLogin/preLogin");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/shibbolethlogin/shibbolethLogin/initLogin/sso");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("selectLocale=ja&mistakeChecker=0&EXCLUDE_SET=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/shibbolethlogin/shibbolethLogin/initLogin/sso");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("POST"), "https://idp.shizuoka.ac.jp/idp/profile/SAML2/Redirect/SSO?execution=e1s1");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"j_username={Unprotect(Account.UserId, null!, DataProtectionScope.CurrentUser)}&j_password={Unprotect(Account.PassWord, null!, DataProtectionScope.CurrentUser)}&_eventId_proceed=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://idp.shizuoka.ac.jp/idp/profile/SAML2/Redirect/SSO?execution=e1s1");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (HttpUtility.HtmlDecode(httpResponseMessage.Content.ReadAsStringAsync().Result).Contains("ユーザ名またはパスワードが正しくありません。") || HttpUtility.HtmlDecode(httpResponseMessage.Content.ReadAsStringAsync().Result).Contains("このサービスを利用するには，静大IDとパスワードが必要です。"))
            {
                Logger.Warn("Return Login by wrong username or password.");
                throw new UnableAuthenticateException();
            }
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            var relayState = ReplaceColon(htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[1]").Attributes["value"].Value);
            var samlResponse = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[2]").Attributes["value"].Value;
            relayState = Uri.EscapeDataString(relayState);
            samlResponse = Uri.EscapeDataString(samlResponse);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/Shibboleth.sso/SAML2/POST");
            httpRequestMessage.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            httpRequestMessage.Headers.TryAddWithoutValidation("Origin", "https://idp.shizuoka.ac.jp");
            httpRequestMessage.Headers.TryAddWithoutValidation("Referer", "https://idp.shizuoka.ac.jp/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"RelayState={relayState}&SAMLResponse={samlResponse}");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/Shibboleth.sso/SAML2/POST");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectSingleNode("//*[@id=\"AdaptiveAuthentication\"]/form/div/input") != null)
            {
                Account.ApacheToken = htmlDocument.DocumentNode
                    .SelectSingleNode("//*[@id=\"AdaptiveAuthentication\"]/form/div/input").Attributes["value"].Value;
                httpRequestMessage = new(new("POST"),
                    "https://gakujo.shizuoka.ac.jp/portal/common/accessEnvironmentRegist/goHome/");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpRequestMessage.Content =
                    new StringContent(
                        $"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&accessEnvName=GakujoGUI {Guid.NewGuid().ToString("N")[..8]}&newAccessKey=");
                httpRequestMessage.Content.Headers.ContentType =
                    MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/accessEnvironmentRegist/goHome/");
                Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                if (cookieContainer.GetAllCookies().Any(x => x.Name.Contains("Access-Environment-Cookie")))
                {
                    Account.AccessEnvironmentKey = cookieContainer.GetAllCookies()
                        .First(x => x.Name.Contains("Access-Environment-Cookie")).Name;
                    Account.AccessEnvironmentValue = cookieContainer.GetAllCookies()
                        .First(x => x.Name.Contains("Access-Environment-Cookie")).Value;
                }
            }
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/home/home/initialize");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("EXCLUDE_SET=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/home/home/initialize");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            Account.StudentName = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/div/div/div/ul[2]/li/a/span/span").InnerText;
            Account.StudentName = Account.StudentName[..^2];
            Account.LoginDateTime = DateTime.Now;
            Logger.Info("End Login.");
            SaveConfigs();
        }

        public void GetNews()
        {
            Logger.Info("Start Get News.");
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/home/home/initialize");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("EXCLUDE_SET=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/home/home/initialize");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/home/changeNewsCondition/initialize");
            httpRequestMessage.Headers.TryAddWithoutValidation("Origin", "https://gakujo.shizuoka.ac.jp");
            httpRequestMessage.Headers.TryAddWithoutValidation("Referer", "https://gakujo.shizuoka.ac.jp/portal/home/home/initialize");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/home/changeNewsCondition/initialize");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/home/changeNewsCondition/confirm");
            httpRequestMessage.Headers.TryAddWithoutValidation("Origin", "https://gakujo.shizuoka.ac.jp");
            httpRequestMessage.Headers.TryAddWithoutValidation("Referer", "https://gakujo.shizuoka.ac.jp/portal/home/changeNewsCondition/initialize");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&contactKind=&contactTitle=&contactDateFrom={ReportDateStart:yyyy/MM/dd}&contactDateTo={ReportDateEnd:yyyy/MM/dd}&contactUserName=&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_Z07_2&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/home/changeNewsCondition/confirm");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/home/home/changeNews");
            httpRequestMessage.Headers.TryAddWithoutValidation("Origin", "https://gakujo.shizuoka.ac.jp");
            httpRequestMessage.Headers.TryAddWithoutValidation("Referer", "https://gakujo.shizuoka.ac.jp/portal/home/changeNewsCondition/initialize");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&_screenIdentifier=home&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/home/home/changeNews");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            var limitCount = htmlDocument.GetElementbyId("tbl_news").SelectSingleNode("tbody").SelectNodes("tr").Count;
            Logger.Info($"Found {limitCount} news.");
            List<News> news = new();
            for (var i = 0; i < limitCount; i++)
            {
                var htmlNodes = htmlDocument.GetElementbyId("tbl_news").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td");
                news.Add(new() { Index = i, Type = htmlNodes[0].InnerText, DateTime = DateTime.Parse(htmlNodes[1].InnerText), Title = htmlNodes[0].InnerText == "学内連絡" ? htmlNodes[2].InnerText : Regex.Replace(htmlNodes[2].InnerText, @"\[.*] ", "") });
            }
            Logger.Info("End Get News.");
            SaveConfigs();
        }

        public void GetReports(out List<Report> diffReports)
        {
            Logger.Info("Start Get Reports.");
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=授業サポート&menuCode=A02&nextPath=/report/student/searchList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/search");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&reportId=&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=&listSubjectCode=&listClassCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A02_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/search");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            if (htmlDocument.GetElementbyId("searchList") == null)
            {
                diffReports = new();
                Logger.Warn("Return Get Reports by not found list.");
                Account.ReportDateTime = DateTime.Now;
                Logger.Info("End Get Reports.");
                ApplyReportsClassTables();
                SaveConfigs();
                return;
            }
            diffReports = new();
            var limitCount = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr").Count;
            Logger.Info($"Found {limitCount} reports.");
            for (var i = 0; i < limitCount; i++)
            {
                var htmlNodes = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td");
                Report report = new()
                {
                    Subjects = ReplaceSpace(htmlNodes[0].InnerText),
                    Title = htmlNodes[1].SelectSingleNode("a").InnerText.Trim(),
                    Id = ReplaceJsArgs(htmlNodes[1].SelectSingleNode("a").Attributes["onclick"].Value, 1),
                    SchoolYear = ReplaceJsArgs(htmlNodes[1].SelectSingleNode("a").Attributes["onclick"].Value, 3),
                    SubjectCode = ReplaceJsArgs(htmlNodes[1].SelectSingleNode("a").Attributes["onclick"].Value, 4),
                    ClassCode = ReplaceJsArgs(htmlNodes[1].SelectSingleNode("a").Attributes["onclick"].Value, 5),
                    Status = htmlNodes[2].InnerText.Trim(),
                    StartDateTime = ReplaceTimeSpan(htmlNodes[3].InnerText, 0),
                    EndDateTime = ReplaceTimeSpan(htmlNodes[3].InnerText, 1)
                };
                if (htmlNodes[4].InnerText.Trim() != "")
                    report.SubmittedDateTime = DateTime.Parse(htmlNodes[4].InnerText.Trim());
                report.ImplementationFormat = htmlNodes[5].InnerText.Trim();
                report.Operation = htmlNodes[6].InnerText.Trim();
                if (!Reports.Contains(report))
                    diffReports.Add(report);
                else
                {
                    Reports.Where(x => x.Id == report.Id && x.ClassCode == report.ClassCode).ToList().ForEach(x =>
                    {
                        x.Title = report.Title;
                        x.Status = report.Status;
                        x.StartDateTime = report.StartDateTime;
                        x.EndDateTime = report.EndDateTime;
                        x.SubmittedDateTime = report.SubmittedDateTime;
                        x.ImplementationFormat = report.ImplementationFormat;
                        x.Operation = report.Operation;
                    });
                }
            }
            Reports.AddRange(diffReports);
            Logger.Info($"Found {diffReports.Count} new Reports.");
            Reports.Where(x => !x.IsAcquired).ToList().ForEach(GetReport);
            Account.ReportDateTime = DateTime.Now;
            Logger.Info("End Get Reports.");
            ApplyReportsClassTables();
            SaveConfigs();
        }

        public void GetReport(Report report)
        {
            Logger.Info($"Start Get Report reportId={report.Id}.");
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=授業サポート&menuCode=A02&nextPath=/report/student/searchList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/search");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&reportId=&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=&listSubjectCode=&listClassCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A02_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/search");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/forwardSubmitRef");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&reportId={report.Id}&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear={schoolYear}&listSubjectCode={report.SubjectCode}&listClassCode={report.ClassCode}&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A02_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/forwardSubmitRef");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            var htmlNodes = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr");
            report.EvaluationMethod = htmlNodes[2].SelectSingleNode("td").InnerText;
            report.Description = ReplaceHtmlNewLine(htmlNodes[3].SelectSingleNode("td").InnerHtml);
            report.Message = ReplaceHtmlNewLine(htmlNodes[5].SelectSingleNode("td").InnerHtml);
            if (htmlNodes[4].SelectSingleNode("td").SelectNodes("a") != null)
            {
                report.Files = new string[htmlNodes[4].SelectSingleNode("td").SelectNodes("a").Count];
                for (var i = 0; i < htmlNodes[4].SelectSingleNode("td").SelectNodes("a").Count; i++)
                {
                    var htmlNode = htmlNodes[4].SelectSingleNode("td").SelectNodes("a")[i];
                    var selectedKey = ReplaceJsArgs(htmlNode.Attributes["onclick"].Value, 0).Replace("fileDownload", "");
                    var prefix = ReplaceJsArgs(htmlNode.Attributes["onclick"].Value, 1);
                    httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classsupport/fileDownload/temporaryFileDownload?EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&selectedKey={selectedKey}&prefix={prefix}&EXCLUDE_SET=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                    Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classsupport/fileDownload/temporaryFileDownload?EXCLUDE_SET=");
                    Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                    var stream = httpResponseMessage.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath))
                        Directory.CreateDirectory(downloadPath);
                    using (var fileStream = File.Create(Path.Combine(downloadPath, htmlNode.InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    report.Files[i] = Path.Combine(downloadPath, htmlNode.InnerText.Trim());
                }
            }
            Logger.Info($"End Get Report reportId={report.Id}.");
            SaveConfigs();
        }

        private void ApplyReportsClassTables()
        {
            Logger.Info("Start Apply Reports to ClassTables.");
            foreach (var classTableRow in ClassTables)
                foreach (var classTableCell in classTableRow)
                    classTableCell.ReportCount = 0;
            foreach (var report in Reports.Where(x => x.IsSubmittable))
                foreach (var classTableRow in ClassTables)
                    foreach (var classTableCell in classTableRow)
                        if (report.Subjects.Contains($"{classTableCell.SubjectsName}（{classTableCell.ClassName}）"))
                            classTableCell.ReportCount++;
            Logger.Info("End Apply Reports to ClassTables");
        }

        public void GetQuizzes(out List<Quiz> diffQuizzes)
        {
            Logger.Info("Start Get Quizzes.");
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=小テスト一覧&menuCode=A03&nextPath=/test/student/searchList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/search");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&testId=&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=&listSubjectCode=&listClassCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A03_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/search");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            if (htmlDocument.GetElementbyId("searchList") == null)
            {
                diffQuizzes = new();
                Logger.Warn("Return Get Quizzes by not found list.");
                Account.QuizDateTime = DateTime.Now;
                Logger.Info("End Get Quizzes.");
                ApplyQuizzesClassTables();
                SaveConfigs();
                return;
            }
            diffQuizzes = new();
            var limitCount = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr").Count;
            Logger.Info($"Found {limitCount} quizzes.");
            for (var i = 0; i < limitCount; i++)
            {
                var htmlNodes = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td");
                Quiz quiz = new()
                {
                    Subjects = ReplaceSpace(htmlNodes[0].InnerText),
                    Title = htmlNodes[1].SelectSingleNode("a").InnerText.Trim(),
                    Id = ReplaceJsArgs(htmlNodes[1].SelectSingleNode("a").Attributes["onclick"].Value, 1),
                    SchoolYear = ReplaceJsArgs(htmlNodes[1].SelectSingleNode("a").Attributes["onclick"].Value, 3),
                    SubjectCode = ReplaceJsArgs(htmlNodes[1].SelectSingleNode("a").Attributes["onclick"].Value, 4),
                    ClassCode = ReplaceJsArgs(htmlNodes[1].SelectSingleNode("a").Attributes["onclick"].Value, 5),
                    Status = htmlNodes[2].InnerText.Trim(),
                    StartDateTime = ReplaceTimeSpan(htmlNodes[3].InnerText, 0),
                    EndDateTime = ReplaceTimeSpan(htmlNodes[3].InnerText, 1),
                    SubmissionStatus = htmlNodes[4].InnerText.Trim(),
                    ImplementationFormat = htmlNodes[5].InnerText.Trim(),
                    Operation = htmlNodes[6].InnerText.Trim()
                };
                if (!Quizzes.Contains(quiz))
                    diffQuizzes.Add(quiz);
                else
                {
                    Quizzes.Where(x => x.Id == quiz.Id && x.ClassCode == quiz.ClassCode).ToList().ForEach(x =>
                    {
                        x.Title = quiz.Title;
                        x.Status = quiz.Status;
                        x.StartDateTime = quiz.StartDateTime;
                        x.EndDateTime = quiz.EndDateTime;
                        x.SubmissionStatus = quiz.SubmissionStatus;
                        x.ImplementationFormat = quiz.ImplementationFormat;
                        x.Operation = quiz.Operation;
                    });
                }
            }
            Quizzes.AddRange(diffQuizzes);
            Logger.Info($"Found {diffQuizzes.Count} new Quizzes.");
            Quizzes.Where(x => !x.IsAcquired).ToList().ForEach(GetQuiz);
            Account.QuizDateTime = DateTime.Now;
            Logger.Info("End Get Quizzes.");
            ApplyQuizzesClassTables();
            SaveConfigs();
        }

        public void GetQuiz(Quiz quiz)
        {
            Logger.Info($"Start Get Quiz quizId={quiz.Id}.");
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=小テスト一覧&menuCode=A03&nextPath=/test/student/searchList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/search");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&testId=&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=&listSubjectCode=&listClassCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A03_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/search");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/forwardSubmitRef");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&testId={quiz.Id}&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear={schoolYear}&listSubjectCode={quiz.SubjectCode}&listClassCode={quiz.ClassCode}&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A03_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/forwardSubmitRef");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            var htmlNodes = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr");
            quiz.QuestionsCount = int.Parse(htmlNodes[2].SelectSingleNode("td").InnerText.Replace("問", "").Trim());
            quiz.EvaluationMethod = htmlNodes[3].SelectSingleNode("td").InnerText;
            quiz.Description = ReplaceHtmlNewLine(htmlNodes[4].SelectSingleNode("td").InnerHtml);
            quiz.Message = ReplaceHtmlNewLine(htmlNodes[6].SelectSingleNode("td").InnerHtml);
            if (htmlNodes[5].SelectSingleNode("td").SelectNodes("a") != null)
            {
                quiz.Files = new string[htmlNodes[5].SelectSingleNode("td").SelectNodes("a").Count];
                for (var i = 0; i < htmlNodes[5].SelectSingleNode("td").SelectNodes("a").Count; i++)
                {
                    var htmlNode = htmlNodes[5].SelectSingleNode("td").SelectNodes("a")[i];
                    var selectedKey = ReplaceJsArgs(htmlNode.Attributes["onclick"].Value, 0).Replace("fileDownload", "");
                    var prefix = ReplaceJsArgs(htmlNode.Attributes["onclick"].Value, 1);
                    httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classsupport/fileDownload/temporaryFileDownload?EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&selectedKey={selectedKey}&prefix={prefix}&EXCLUDE_SET=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                    Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classsupport/fileDownload/temporaryFileDownload?EXCLUDE_SET=");
                    Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                    var stream = httpResponseMessage.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath)) { Directory.CreateDirectory(downloadPath); }
                    using (var fileStream = File.Create(Path.Combine(downloadPath, htmlNode.InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    quiz.Files[i] = Path.Combine(downloadPath, htmlNode.InnerText.Trim());
                }
            }
            Logger.Info($"End Get Quiz quizId={quiz.Id}.");
            SaveConfigs();
        }

        private void ApplyQuizzesClassTables()
        {
            Logger.Info("Start Apply Quizzes to ClassTables.");
            foreach (var classTableRow in ClassTables)
                foreach (var classTableCell in classTableRow)
                    classTableCell.QuizCount = 0;
            foreach (var quiz in Quizzes.Where(x => x.IsSubmittable))
                foreach (var classTableRow in ClassTables)
                    foreach (var classTableCell in classTableRow)
                        if (quiz.Subjects.Contains($"{classTableCell.SubjectsName}（{classTableCell.ClassName}）"))
                            classTableCell.QuizCount++;
            Logger.Info("End Apply Quizzes to ClassTables.");
        }

        public void GetClassContacts(out int diffCount, int maxCount = 10)
        {
            Logger.Info("Start Get ClassContacts.");
            var lastClassContact = ClassContacts.Count > 0 ? ClassContacts[0] : null;
            List<ClassContact> diffClassContacts = new();
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=授業連絡一覧&menuCode=A01&nextPath=/classcontact/classContactList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/selectClassContactList");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&teacherCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&searchKeyWord=&checkSearchKeywordTeacherUserName=on&checkSearchKeywordSubjectName=on&checkSearchKeywordTitle=on&contactKindCode=&targetDateStart=&targetDateEnd=&reportDateStart={ReportDateStart:yyyy/MM/dd}");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/selectClassContactList");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            if (htmlDocument.GetElementbyId("tbl_A01_01") == null)
            {
                diffCount = 0;
                Logger.Warn("Return Get ClassContacts by not found list.");
                Account.ClassContactDateTime = DateTime.Now;
                Logger.Info("End Get ClassContacts.");
                SaveConfigs();
                return;
            }
            var limitCount = htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr").Count;
            Logger.Info($"Found {limitCount} ClassContacts.");
            for (var i = 0; i < limitCount; i++)
            {
                var htmlNodes = htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td");
                ClassContact classContact = new()
                {
                    Subjects = ReplaceSpace(htmlNodes[1].InnerText),
                    TeacherName = htmlNodes[2].InnerText.Trim(),
                    Title = HttpUtility.HtmlDecode(htmlNodes[3].SelectSingleNode("a").InnerText).Trim()
                };
                if (htmlNodes[5].InnerText.Trim() != "")
                    classContact.TargetDateTime = DateTime.Parse(htmlNodes[5].InnerText.Trim());
                classContact.ContactDateTime = DateTime.Parse(htmlNodes[6].InnerText.Trim());
                if (classContact.Equals(lastClassContact))
                {
                    Logger.Info("Break by equals last ClassContact.");
                    break;
                }
                diffClassContacts.Add(classContact);
            }
            diffCount = diffClassContacts.Count;
            Logger.Info($"Found {diffCount} new ClassContacts.");
            ClassContacts.InsertRange(0, diffClassContacts);
            maxCount = maxCount == -1 ? diffCount : maxCount;
            for (var i = 0; i < Math.Min(diffCount, maxCount); i++)
                GetClassContact(i);
            Account.ClassContactDateTime = DateTime.Now;
            Logger.Info("End Get ClassContacts.");
            SaveConfigs();
        }

        private static string GetEmbedLinks(string value)
        {
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(value);
            if (htmlDocument.DocumentNode.SelectNodes("//a") == null)
                return "";
            var links = htmlDocument.DocumentNode.SelectNodes("//a").Aggregate("", (current, linkNode) => current + $"\r\n{linkNode.InnerText} {linkNode.Attributes["href"].Value}  ");
            return links;
        }

        public void GetClassContact(int indexCount)
        {
            Logger.Info($"Start Get ClassContact indexCount={indexCount}.");
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=授業連絡一覧&menuCode=A01&nextPath=/classcontact/classContactList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/selectClassContactList");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&teacherCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&searchKeyWord=&checkSearchKeywordTeacherUserName=on&checkSearchKeywordSubjectName=on&checkSearchKeywordTitle=on&contactKindCode=&targetDateStart=&targetDateEnd=&reportDateStart={ReportDateStart:yyyy/MM/dd}");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/selectClassContactList");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/goDetail/" + indexCount);
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            var content = $"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&teacherCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&searchKeyWord=&checkSearchKeywordTeacherUserName=on&checkSearchKeywordSubjectName=on&checkSearchKeywordTitle=on&contactKindCode=&targetDateStart=&targetDateEnd=&reportDateStart={schoolYear}/01/01&reportDateEnd=&requireResponse=&studentCode=&studentName=&tbl_A01_01_length=-1&_searchConditionDisp.accordionSearchCondition=false&_screenIdentifier=SC_A01_01&_screenInfoDisp=true&_scrollTop=0";
            httpRequestMessage.Content = new StringContent(content);
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/goDetail/" + indexCount);
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            var htmlNodes = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr");
            ClassContacts[indexCount].ContactType = htmlNodes[0].SelectSingleNode("td").InnerText;
            var offset = 0;
            if (ClassContacts[indexCount].ContactType == "講義室変更")
                offset = 2;
            else if (ClassContacts[indexCount].ContactType == "休講")
                offset = 1;
            ClassContacts[indexCount].Content = ReplaceHtmlNewLine(htmlNodes[2 + offset].SelectSingleNode("td").InnerText);
            if (GetEmbedLinks(htmlNodes[2 + offset].SelectSingleNode("td").InnerHtml) != "")
                ClassContacts[indexCount].Content += $"\r\n\r\n埋込リンク{GetEmbedLinks(htmlNodes[2 + offset].SelectSingleNode("td").InnerHtml)}";
            if (ClassContacts[indexCount].ContactType == "講義室変更")
            {
                ClassContacts[indexCount].Content += "\r\n";
                ClassContacts[indexCount].Content +=
                    $"\r\n講義室変更日 {ReplaceSpace(htmlNodes[1].SelectSingleNode("td").InnerText)}";
                ClassContacts[indexCount].Content +=
                    $"\r\n変更後講義室 {ReplaceSpace(htmlNodes[2].SelectSingleNode("td").InnerText)}";
            }
            else if (ClassContacts[indexCount].ContactType == "休講")
            {
                ClassContacts[indexCount].Content += "\r\n";
                ClassContacts[indexCount].Content +=
                    $"\r\n休講日 {ReplaceSpace(htmlNodes[1].SelectSingleNode("td").InnerText)}";
            }
            ClassContacts[indexCount].FileLinkRelease = ReplaceSpace(htmlNodes[4 + offset].SelectSingleNode("td").InnerText);
            ClassContacts[indexCount].ReferenceUrl = ReplaceSpace(htmlNodes[5 + offset].SelectSingleNode("td").InnerText);
            ClassContacts[indexCount].Severity = ReplaceSpace(htmlNodes[6 + offset].SelectSingleNode("td").InnerText);
            ClassContacts[indexCount].WebReplyRequest = htmlNodes[8 + offset].SelectSingleNode("td").InnerText;
            ClassContacts[indexCount].Files = Array.Empty<string>();
            if (htmlNodes[3 + offset].SelectSingleNode("td/div").SelectNodes("div") != null)
            {
                ClassContacts[indexCount].Files = new string[htmlNodes[3 + offset].SelectSingleNode("td/div").SelectNodes("div").Count];
                for (var i = 0; i < htmlNodes[3 + offset].SelectSingleNode("td/div").SelectNodes("div").Count; i++)
                {
                    var htmlNode = htmlNodes[3 + offset].SelectSingleNode("td/div").SelectNodes("div")[i];
                    var prefix = ReplaceJsArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 0).Replace("fileDownLoad", "");
                    var no = ReplaceJsArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 1);
                    httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + $"{prefix}&no={no}&EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&prefix=default&sequence=&webspaceTabDisplayFlag=&screenName=&fileNameAutonumberFlag=&fileNameDisplayFlag=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                    Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + $"{prefix}&no={no}&EXCLUDE_SET=");
                    Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                    var stream = httpResponseMessage.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath))
                        Directory.CreateDirectory(downloadPath);
                    using (var fileStream = File.Create(Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    ClassContacts[indexCount].Files[i] = Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim());
                }
            }
            Logger.Info($"End Get ClassContact indexCount={indexCount}.");
            SaveConfigs();
        }

        public void GetClassSharedFiles(out int diffCount)
        {
            Logger.Info("Start Get ClassSharedFiles.");
            Dictionary<int, ClassSharedFile> diffClassSharedFiles = new();
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=授業共有ファイル&menuCode=A08&nextPath=/classfile/classFile/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/selectClassFileList");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&searchKeyWord=&searchScopeTitle=Y&lastUpdateDate=&tbl_classFile_length=-1&linkDetailIndex=0&selectIndex=&prevPageId=backToList&confirmMsg=&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A08_01&_screenInfoDisp=true&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/selectClassFileList");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            if (htmlDocument.GetElementbyId("tbl_classFile") == null)
            {
                diffCount = 0;
                Logger.Warn("Return Get ClassSharedFiles by not found list.");
                Account.ClassSharedFileDateTime = DateTime.Now;
                Logger.Info("End Get ClassSharedFiles.");
                SaveConfigs();
                return;
            }
            var limitCount = htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr").Count;
            Logger.Info($"Found {limitCount} ClassSharedFiles.");
            for (var i = 0; i < limitCount; i++)
            {
                var htmlNodes = htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td");
                ClassSharedFile classSharedFile = new()
                {
                    Subjects = ReplaceSpace(htmlNodes[1].InnerText),
                    Title = HttpUtility.HtmlDecode(htmlNodes[2].SelectSingleNode("a").InnerText).Trim(),
                    Size = htmlNodes[3].InnerText,
                    UpdateDateTime = DateTime.Parse(htmlNodes[4].InnerText)
                };
                if (!ClassSharedFiles.Contains(classSharedFile))
                    diffClassSharedFiles.Add(i, classSharedFile);
            }
            diffCount = diffClassSharedFiles.Count;
            Logger.Info($"Found {diffCount} new ClassSharedFiles.");
            foreach (var diffClassSharedFile in diffClassSharedFiles)
                GetClassSharedFile(diffClassSharedFile.Key, diffClassSharedFile.Value);
            ClassSharedFiles.InsertRange(0, diffClassSharedFiles.Values);
            Account.ClassSharedFileDateTime = DateTime.Now;
            Logger.Info("End Get ClassSharedFiles.");
            SaveConfigs();
        }

        public void GetClassSharedFile(int indexCount, ClassSharedFile classSharedFile)
        {
            Logger.Info($"Start Get ClassSharedFile indexCount={indexCount}.");
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=授業共有ファイル&menuCode=A08&nextPath=/classfile/classFile/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/selectClassFileList");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&searchKeyWord=&searchScopeTitle=Y&lastUpdateDate=&tbl_classFile_length=-1&linkDetailIndex=0&selectIndex=&prevPageId=backToList&confirmMsg=&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A08_01&_screenInfoDisp=true&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/selectClassFileList");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            httpRequestMessage = new(new("POST"), $"https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/showClassFileDetail/{indexCount}");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Headers.TryAddWithoutValidation("Referer", "https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/selectClassFileList");
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&searchKeyWord=&searchScopeTitle=Y&lastUpdateDate=&tbl_classFile_length=-1&linkDetailIndex=0&linkDetailIndex=1&linkDetailIndex=2&linkDetailIndex=3&linkDetailIndex=4&linkDetailIndex=5&linkDetailIndex=6&linkDetailIndex=7&linkDetailIndex=8&linkDetailIndex=9&selectIndex=&prevPageId=backToList&confirmMsg=&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A08_01&_screenInfoDisp=true&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info($"POST https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/showClassFileDetail/{indexCount}");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            GetApacheToken(htmlDocument);
            var htmlNodes = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr");
            classSharedFile.Description = HttpUtility.HtmlDecode(htmlNodes[2].SelectSingleNode("td").InnerText);
            classSharedFile.PublicPeriod = ReplaceSpace(htmlNodes[3].SelectSingleNode("td").InnerText);
            if (htmlNodes[1].SelectSingleNode("td/div") != null)
            {
                classSharedFile.Files = new string[htmlNodes[1].SelectSingleNode("td/div").SelectNodes("div").Count];
                for (var i = 0; i < htmlNodes[1].SelectSingleNode("td/div").SelectNodes("div").Count; i++)
                {
                    var htmlNode = htmlNodes[1].SelectSingleNode("td/div").SelectNodes("div")[i];
                    var prefix = ReplaceJsArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 0).Replace("fileDownLoad", "");
                    var no = ReplaceJsArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 1);
                    httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + $"{prefix}&no={no}&EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&prefix=default&sequence=&webspaceTabDisplayFlag=&screenName=&fileNameAutonumberFlag=&fileNameDisplayFlag=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                    Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + $"{prefix}&no={no}&EXCLUDE_SET=");
                    Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                    var stream = httpResponseMessage.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath))
                        Directory.CreateDirectory(downloadPath);
                    using (var fileStream = File.Create(Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    classSharedFile.Files[i] = Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim());
                }
            }
            Logger.Info($"End Get ClassSharedFile indexCount={indexCount}.");
            SaveConfigs();
        }

        private bool SetAcademicSystem(out bool lotteryRegistrationEnabled, out bool lotteryRegistrationResultEnabled, out bool generalRegistrationEnabled)
        {
            Logger.Info("Start Set AcademicSystem.");
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/preLogin.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/preLogin.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/home/systemCooperationLink/initializeShibboleth?renkeiType=kyoumu");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Headers.TryAddWithoutValidation("Origin", "https://gakujo.shizuoka.ac.jp");
            httpRequestMessage.Headers.TryAddWithoutValidation("Referer", "https://gakujo.shizuoka.ac.jp/portal/home/home/initialize");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/home/systemCooperationLink/initializeShibboleth?renkeiType=kyoumu");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/sso/loginStudent.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("loginID=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/sso/loginStudent.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/form/div/input[1]") != null && htmlDocument.DocumentNode.SelectNodes("/html/body/form/div/input[2]") != null)
            {
                Logger.Warn("Additional transition.");
                var relayState = ReplaceColon(htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[1]").Attributes["value"].Value);
                var samlResponse = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[2]").Attributes["value"].Value;
                relayState = Uri.EscapeDataString(relayState);
                samlResponse = Uri.EscapeDataString(samlResponse);
                httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/Shibboleth.sso/SAML2/POST");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpRequestMessage.Content = new StringContent($"RelayState={relayState}&SAMLResponse={samlResponse}");
                httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                Logger.Info("POST https://gakujo.shizuoka.ac.jp/Shibboleth.sso/SAML2/POST");
                Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            }
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            lotteryRegistrationEnabled = htmlDocument.DocumentNode.SelectNodes("//a[contains(@onclick,\"mainMenuCode=019&parentMenuCode=001\")]") != null;
            lotteryRegistrationResultEnabled = htmlDocument.DocumentNode.SelectNodes("//a[contains(@onclick,\"mainMenuCode=020&parentMenuCode=001\")]") != null;
            generalRegistrationEnabled = htmlDocument.DocumentNode.SelectNodes("//a[contains(@onclick,\"mainMenuCode=002&parentMenuCode=001\")]") != null;
            Logger.Info("End Set AcademicSystem.");
            SaveConfigs();
            return htmlDocument.DocumentNode.SelectNodes("//a[contains(@onclick,\"mainMenuCode=008&parentMenuCode=007\")]") != null;
        }

        public void GetLotteryRegistrations(out string jikanwariVector)
        {
            Logger.Info("Start Get LotteryRegistrations.");
            jikanwariVector = "AA";
            if (!SetAcademicSystem(out var lotteryRegistrationEnabled, out _, out _))
            {
                Logger.Warn("Not found AcademicSystem by overtime.");
                return;
            }
            if (!lotteryRegistrationEnabled)
            {
                Logger.Warn("Not found LotteryRegistrations by overtime.");
                return;
            }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuInit.do?mainMenuCode=019&parentMenuCode=001");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuInit.do?mainMenuCode=019&parentMenuCode=001");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/form") == null)
            {
                Logger.Warn("Not found LotteryRegistrations.");
                return;
            }
            foreach (var classTableRow in ClassTables)
                foreach (var classTableCell in classTableRow)
                    classTableCell.LotteryRegistrations.Clear();
            jikanwariVector = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/input").Attributes["value"].Value;
            for (var i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table").Count; i++)
            {
                List<LotteryRegistration> lotteryRegistrations = new();
                var htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table")[i].SelectSingleNode("tr/td/table");
                if (htmlNode == null)
                    continue;
                for (var j = 2; j < htmlNode.SelectNodes("tr").Count; j++)
                {
                    LotteryRegistration lotteryRegistration = new()
                    {
                        WeekdayPeriod = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[0].InnerText),
                        SubjectsName = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[1].InnerText),
                        ClassName = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[2].InnerText),
                        SubjectsSection = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[3].InnerText),
                        SelectionSection = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[4].InnerText),
                        Credit = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[5].InnerText.Trim()),
                        IsRegisterable = !htmlNode.SelectNodes("tr")[j].SelectNodes("td")[6].SelectSingleNode("input").Attributes.Contains("disabled"),
                        AttendingCapacity = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[10].InnerText.Replace("&nbsp;", "").Trim()),
                        FirstApplicantNumber = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[11].InnerText.Replace("&nbsp;", "").Trim()),
                        SecondApplicantNumber = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[12].InnerText.Replace("&nbsp;", "").Trim()),
                        ThirdApplicantNumber = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[13].InnerText.Replace("&nbsp;", "").Trim()),
                        ChoiceNumberKey = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[6].SelectSingleNode("input").Attributes["name"].Value
                    };
                    if (htmlNode.SelectNodes("tr")[j].SelectNodes("td")[6].SelectSingleNode("input").Attributes.Contains("checked"))
                        lotteryRegistration.ChoiceNumberValue = 0;
                    else if (htmlNode.SelectNodes("tr")[j].SelectNodes("td")[7].SelectSingleNode("input").Attributes.Contains("checked"))
                        lotteryRegistration.ChoiceNumberValue = 1;
                    else if (htmlNode.SelectNodes("tr")[j].SelectNodes("td")[8].SelectSingleNode("input").Attributes.Contains("checked"))
                        lotteryRegistration.ChoiceNumberValue = 2;
                    else if (htmlNode.SelectNodes("tr")[j].SelectNodes("td")[9].SelectSingleNode("input").Attributes.Contains("checked"))
                        lotteryRegistration.ChoiceNumberValue = 3;
                    lotteryRegistrations.Add(lotteryRegistration);
                }
                ClassTables[ReplacePeriod(lotteryRegistrations[0].WeekdayPeriod)][ReplaceWeekday(lotteryRegistrations[0].WeekdayPeriod)].LotteryRegistrations = lotteryRegistrations;
            }
            Logger.Info("End Get LotteryRegistrations.");
            Account.LotteryRegistrationDateTime = DateTime.Now;
            SaveConfigs();
        }

        public void SetLotteryRegistrations(List<LotteryRegistrationEntry> lotteryRegistrationEntries, bool notifyMail = false)
        {
            Logger.Info("Start Set LotteryRegistrations.");
            if (!SetAcademicSystem(out var lotteryRegistrationEnabled, out _, out _))
            {
                Logger.Warn("Not found AcademicSystem by overtime.");
                return;
            }
            if (!lotteryRegistrationEnabled)
            {
                Logger.Warn("Return Set LotteryRegistrations by overtime.");
                return;
            }
            GetLotteryRegistrations(out var jikanwariVector);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuRegist.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            var choiceNumbers = "";
            foreach (var lotteryRegistrationEntry in lotteryRegistrationEntries)
                foreach (var lotteryRegistrations in LotteryRegistrations.Where(lotteryRegistrations => lotteryRegistrations.Count(x =>
                             x.SubjectsName == lotteryRegistrationEntry.SubjectsName &&
                             x.ClassName == lotteryRegistrationEntry.ClassName && x.IsRegisterable) == 1))
                {
                    lotteryRegistrations.Where(x => x.ChoiceNumberValue == lotteryRegistrationEntry.AspirationOrder).ToList().ForEach(x => x.ChoiceNumberValue = 0);
                    lotteryRegistrations.First(x => x.SubjectsName == lotteryRegistrationEntry.SubjectsName && x.ClassName == lotteryRegistrationEntry.ClassName && x.IsRegisterable).ChoiceNumberValue = lotteryRegistrationEntry.AspirationOrder;
                }
            LotteryRegistrations.SelectMany(_ => _).Where(x => x.IsRegisterable).ToList().ForEach(x => { choiceNumbers += x.ToChoiceNumberString(); Logger.Info($"ChoiceNumber {x.ToChoiceNumberString()}"); });
            httpRequestMessage.Content = new StringContent($"x=0&y=0&RishuuForm.jikanwariVector={jikanwariVector}{choiceNumbers}");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuRegist.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            var selectedSemesterCode = ReplaceJsArgs(htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/table[1]/tbody/tr/td[2]/a").Attributes["href"].Value, 1);
            if (notifyMail)
            {
                httpRequestMessage = new(new("GET"), $"https://gakujo.shizuoka.ac.jp/kyoumu/sendChuusenRishuuMailInit.do?selectedSemesterCode={selectedSemesterCode}");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                Logger.Info($"GET https://gakujo.shizuoka.ac.jp/kyoumu/sendChuusenRishuuMailInit.do?selectedSemesterCode={selectedSemesterCode}");
                Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
                var mailAddress = htmlDocument.DocumentNode.SelectSingleNode("//input[@name='mailAddress' and @checked]").Attributes["value"].Value;
                httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/sendChuusenRishuuMail.do");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpRequestMessage.Content = new StringContent($"{mailAddress}&button_changePassword.changePassword.x=0&button_changePassword.changePassword.y=0");
                httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                Logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/sendChuusenRishuuMail.do");
                Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            }
            Logger.Info("End Set LotteryRegistrations.");
            SaveConfigs();
        }

        public void GetLotteryRegistrationsResult()
        {
            Logger.Info("Start Get LotteryRegistrationsResult.");
            if (!SetAcademicSystem(out _, out var lotteryRegistrationResultEnabled, out _))
            {
                Logger.Warn("Not found AcademicSystem by overtime.");
                return;
            }
            if (!lotteryRegistrationResultEnabled)
            {
                Logger.Warn("Not found LotteryRegistrationsResult by overtime.");
                return;
            }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuInit.do?mainMenuCode=020&parentMenuCode=001");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuInit.do?mainMenuCode=020&parentMenuCode=001");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/form") == null) { Logger.Warn("Not found LotteryRegistrationsResult."); return; }
            foreach (var classTableRow in ClassTables)
                foreach (var classTableCell in classTableRow)
                    classTableCell.LotteryRegistrationsResult.Clear();
            for (var i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table").Count; i++)
            {
                List<LotteryRegistrationResult> lotteryRegistrationsResult = new();
                var htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table")[i].SelectSingleNode("tr/td/table");
                if (htmlNode == null)
                    continue;
                for (var j = 1; j < htmlNode.SelectNodes("tr").Count; j++)
                {
                    LotteryRegistrationResult lotteryRegistrationResult = new()
                    {
                        WeekdayPeriod = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[0].InnerText),
                        SubjectsName = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[1].InnerText),
                        ClassName = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[2].InnerText),
                        SubjectsSection = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[3].InnerText),
                        SelectionSection = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[4].InnerText),
                        Credit = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[5].InnerText.Trim()),
                        ChoiceNumberValue = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[6].InnerText.Replace("&nbsp;", "").Trim()),
                        IsWinning = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[7].InnerText.Contains("当選")
                    };
                    lotteryRegistrationsResult.Add(lotteryRegistrationResult);
                }
                ClassTables[ReplacePeriod(lotteryRegistrationsResult[0].WeekdayPeriod)][ReplaceWeekday(lotteryRegistrationsResult[0].WeekdayPeriod)].LotteryRegistrationsResult = lotteryRegistrationsResult;
            }
            Logger.Info("End Get LotteryRegistrationsResult.");
            Account.LotteryRegistrationResultDateTime = DateTime.Now;
            SaveConfigs();
        }

        public void GetGeneralRegistrations()
        {
            Logger.Info("Start Get GeneralRegistrations.");
            if (!SetAcademicSystem(out _, out _, out var generalRegistrationEnabled))
            {
                Logger.Warn("Not found AcademicSystem by overtime.");
                return;
            }
            if (!generalRegistrationEnabled)
            {
                Logger.Warn("Not found GeneralRegistrations by overtime.");
                return;
            }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[3]/tr/td/font[1]/b") != null) { Logger.Warn("Not found GeneralRegistrations."); return; }
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]") == null) { Logger.Warn("Not found GeneralRegistrations."); return; }
            foreach (var classTableRow in ClassTables)
                foreach (var classTableCell in classTableRow)
                    classTableCell.GeneralRegistrations = new();
            for (var i = 0; i < 7; i++)
            {
                var htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]/tr/td/table").SelectNodes("tr")[i + 1];
                for (var j = 0; j < 5; j++)
                {
                    GeneralRegistrations generalRegistrations = new()
                    {
                        EntriedGeneralRegistration =
                        {
                            WeekdayPeriod = ReplaceWeekday(j) + ReplacePeriod(i)
                        }
                    };
                    if (htmlNode.SelectNodes("td")[j + 1].SelectSingleNode("a") != null)
                    {
                        generalRegistrations.EntriedGeneralRegistration.SubjectsName = ReplaceSpace(htmlNode.SelectNodes("td")[j + 1].SelectSingleNode("a").InnerText);
                        generalRegistrations.EntriedGeneralRegistration.TeacherName = ReplaceSpace(htmlNode.SelectNodes("td")[j + 1].SelectNodes("text()")[0].InnerText);
                        generalRegistrations.EntriedGeneralRegistration.SelectionSection = ReplaceSpace(htmlNode.SelectNodes("td")[j + 1].SelectNodes("font")[0].InnerText);
                        generalRegistrations.EntriedGeneralRegistration.Credit = int.Parse(htmlNode.SelectNodes("td")[j + 1].SelectNodes("text()")[1].InnerText.Trim().Replace("単位", ""));
                        generalRegistrations.EntriedGeneralRegistration.ClassRoom = ReplaceSpace(htmlNode.SelectNodes("td")[j + 1].SelectNodes("text()")[2].InnerText);
                    }
                    ClassTables[i][j].GeneralRegistrations = generalRegistrations;
                }
            }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/searchKamokuNameInit.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/searchKamokuNameInit.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            var faculty = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[0].Attributes["value"].Value;
            var department = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[1].Attributes["value"].Value;
            var course = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[2].Attributes["value"].Value;
            var grade = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[3].Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/searchKamokuName.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"faculty={faculty}&department={department}&course={course}&grade={grade}&kamokuKbnCode=&req=&kamokuName=&button_kind.search.x=0&button_kind.search.y=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/searchKamokuName.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/table[4]") == null)
                Logger.Warn("Not found RegisterableGeneralRegistrations.");
            else
            {
                for (var i = 1; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/table[4]/tr/td/table").SelectNodes("tr").Count; i++)
                {
                    var htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/table[4]/tr/td/table").SelectNodes("tr")[i];
                    GeneralRegistration generalRegistration = new()
                    {
                        SubjectsName = ReplaceSpace(htmlNode.SelectNodes("td")[1].InnerText),
                        TeacherName = ReplaceSpace(htmlNode.SelectNodes("td")[2].InnerText.Replace("\n", "")),
                        Credit = int.Parse(htmlNode.SelectNodes("td")[3].InnerText.Trim().Replace("単位", ""))
                    };
                    if (htmlNode.SelectNodes("td")[4].Attributes["colspan"] != null)
                    {
                        generalRegistration.WeekdayPeriod = ReplaceSpace(htmlNode.SelectNodes("td")[4].InnerText);
                        generalRegistration.ClassRoom = ReplaceSpace(htmlNode.SelectNodes("td")[5].InnerText);
                    }
                    else
                    {
                        generalRegistration.WeekdayPeriod = ReplaceSpace(htmlNode.SelectNodes("td")[4].InnerText);
                        generalRegistration.WeekdayPeriod += ReplaceSpace(htmlNode.SelectNodes("td")[5].InnerText).Replace("限", "");
                        generalRegistration.ClassRoom = ReplaceSpace(htmlNode.SelectNodes("td")[6].InnerText);
                    }
                    generalRegistration.KamokuCode = ReplaceJsArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 0).Replace("javascript:checkKamoku", "");
                    generalRegistration.ClassCode = ReplaceJsArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 1);
                    generalRegistration.Unit = ReplaceJsArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 2);
                    generalRegistration.SelectKamoku = ReplaceJsArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 3);
                    generalRegistration.Radio = htmlNode.SelectNodes("td")[0].SelectSingleNode("a/input").Attributes["value"].Value;
                    if (generalRegistration.WeekdayPeriod != "時間割外")
                        ClassTables[ReplacePeriod(generalRegistration.WeekdayPeriod)][ReplaceWeekday(generalRegistration.WeekdayPeriod)].GeneralRegistrations.RegisterableGeneralRegistrations.Add(generalRegistration);
                }
            }
            Logger.Info("End Get GeneralRegistrations.");
            Account.GeneralRegistrationDateTime = DateTime.Now;
            SaveConfigs();
        }

        private List<GeneralRegistration> GetRegisterableGeneralRegistrations(string youbi, string jigen, out string faculty, out string department, out string course, out string grade)
        {
            Logger.Info($"Start Get RegisterableGeneralRegistrations youbi={youbi}, jigen={jigen}.");
            List<GeneralRegistration> registerableGeneralRegistrations = new();
            httpRequestMessage = new(new("GET"), $"https://gakujo.shizuoka.ac.jp/kyoumu/searchKamokuInit.do?youbi={youbi}&jigen={jigen}");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info($"GET https://gakujo.shizuoka.ac.jp/kyoumu/searchKamokuInit.do?youbi={youbi}&jigen={jigen}");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            faculty = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[0].Attributes["value"].Value;
            department = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[1].Attributes["value"].Value;
            course = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[2].Attributes["value"].Value;
            grade = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[3].Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/searchKamoku.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"faculty={faculty}&departmen={department}&course={course}&grade={grade}&kamokuKbnCode=&req=&button_kind.search.x=0&button_kind.search.y=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/searchKamoku.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/table[4]") == null) { Logger.Warn("Not found RegisterableGeneralRegistrations."); return registerableGeneralRegistrations; }
            for (var i = 1; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/table[4]/tr/td/table").SelectNodes("tr").Count; i++)
            {
                var htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/table[4]/tr/td/table").SelectNodes("tr")[i];
                GeneralRegistration generalRegistration = new()
                {
                    SubjectsName = ReplaceSpace(htmlNode.SelectNodes("td")[1].InnerText),
                    TeacherName = ReplaceSpace(htmlNode.SelectNodes("td")[2].InnerText.Replace("\n", "")),
                    Credit = int.Parse(htmlNode.SelectNodes("td")[3].InnerText.Trim().Replace("単位", "")),
                    WeekdayPeriod = ReplaceSpace(htmlNode.SelectNodes("td")[4].InnerText)
                };
                generalRegistration.WeekdayPeriod += ReplaceSpace(htmlNode.SelectNodes("td")[5].InnerText).Replace("限", "");
                generalRegistration.ClassRoom = ReplaceSpace(htmlNode.SelectNodes("td")[6].InnerText);
                generalRegistration.KamokuCode = ReplaceJsArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 0).Replace("javascript:checkKamoku", "");
                generalRegistration.ClassCode = ReplaceJsArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 1);
                generalRegistration.Unit = ReplaceJsArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 2);
                generalRegistration.SelectKamoku = ReplaceJsArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 3);
                generalRegistration.Radio = htmlNode.SelectNodes("td")[0].SelectSingleNode("a/input").Attributes["value"].Value;
                registerableGeneralRegistrations.Add(generalRegistration);
            }
            Logger.Info($"End Get RegisterableGeneralRegistrations youbi={youbi}, jigen={jigen}.");
            return registerableGeneralRegistrations;

        }

        private void SetGeneralRegistration(GeneralRegistrationEntry generalRegistrationEntry, bool restore, out int result)
        {
            result = -1;
            var youbi = (ReplaceWeekday(generalRegistrationEntry.WeekdayPeriod) + 1).ToString();
            var jigen = ReplacePeriod(generalRegistrationEntry.WeekdayPeriod).ToString();
            var suggestGeneralRegistrationEntries = GetRegisterableGeneralRegistrations(youbi, jigen, out var faculty, out var department, out var course, out var grade).Where(x => (!restore && x.SubjectsName.Contains(generalRegistrationEntry.SubjectsName) && x.SubjectsName.Contains(generalRegistrationEntry.ClassName)) || (restore && x.KamokuCode == generalRegistrationEntry.EntriedKamokuCode && x.ClassCode == generalRegistrationEntry.EntriedClassCode)).ToList();
            if (suggestGeneralRegistrationEntries.Count != 1)
            {
                Logger.Warn("Not found GeneralRegistration by count not 1.");
                return;
            }
            var generalRegistration = suggestGeneralRegistrationEntries[0];
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/searchKamoku.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"faculty={faculty}&department={department}&course={course}&grade={grade}&kamokuKbnCode=&req=&kamokuCode={generalRegistration.KamokuCode}&classCode={generalRegistration.ClassCode}&unit={generalRegistration.Unit}&radio={generalRegistration.Radio}&selectKamoku={generalRegistration.SelectKamoku}&button_kind.registKamoku.x=0&button_kind.registKamoku.y=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/searchKamoku.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/font[1]/b") != null)
            {
                var errorMessage = htmlDocument.DocumentNode.SelectSingleNode("/html/body/font[2]/ul/li").InnerText;
                if (errorMessage.Contains("他の科目を取り消して、半期履修制限単位数以内で履修登録してください。"))
                {
                    Logger.Error($"Error Set GeneralRegistration {generalRegistration} by credits limit.");
                    result = 1;
                }
                else if (errorMessage.Contains("を取り消してから、履修登録してください。"))
                {
                    Logger.Error($"Error Set GeneralRegistration {generalRegistration} by duplicate class.");
                    result = 2;
                }
                else if (errorMessage.Contains("定員数を超えているため、登録できません。"))
                {
                    Logger.Error($"Error Set GeneralRegistration {generalRegistration} by attending capacity.");
                    result = 3;
                }
                return;
            }
            Logger.Info($"Set GeneralRegistration {generalRegistration}");
            SaveConfigs();
            result = 0;
        }

        private void SetGeneralRegistrationClear(string youbi, string jigen)
        {
            Logger.Info("Start Set GeneralRegistrationClear.");
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[3]/tr/td/font[1]/b") != null)
            {
                Logger.Warn("Not found GeneralRegistrations.");
                return;
            }
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]") == null)
            {
                Logger.Warn("Not found GeneralRegistrations.");
                return;
            }
            var htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]/tr/td/table").SelectNodes("tr")[int.Parse(jigen)].SelectNodes("td")[int.Parse(youbi)].SelectSingleNode("table/tr[2]/td/a");
            if (htmlNode == null)
            {
                Logger.Warn("Not found class in GeneralRegistrations.");
                return;
            }
            var kamokuCode = ReplaceJsArgs(htmlNode.Attributes["href"].Value, 1);
            var classCode = ReplaceJsArgs(htmlNode.Attributes["href"].Value, 2);
            httpRequestMessage = new(new("GET"), $"https://gakujo.shizuoka.ac.jp/kyoumu/removeKamokuInit.do?kamokuCode={kamokuCode}&classCode={classCode}&youbi={youbi}&jigen={jigen}");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info($"GET https://gakujo.shizuoka.ac.jp/kyoumu/removeKamokuInit.do?kamokuCode={kamokuCode}&classCode={classCode}&youbi={youbi}&jigen={jigen}");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/removeKamoku.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/removeKamoku.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Logger.Info("End Set GeneralRegistrationClear.");
            SaveConfigs();
        }

        public void SetGeneralRegistrations(List<GeneralRegistrationEntry> generalRegistrationEntries, bool overwrite = false)
        {
            Logger.Info("Start Set GeneralRegistrations.");
            if (!SetAcademicSystem(out _, out _, out var generalRegistrationEnabled))
            {
                Logger.Warn("Not found AcademicSystem by overtime.");
                return;
            }
            if (!generalRegistrationEnabled)
            {
                Logger.Warn("Return Set GeneralRegistration by overtime.");
                return;
            }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]") == null)
            {
                Logger.Warn("Not found GeneralRegistrations.");
                return;
            }
            foreach (var generalRegistrationEntry in generalRegistrationEntries)
            {
                var youbi = (ReplaceWeekday(generalRegistrationEntry.WeekdayPeriod) + 1).ToString();
                var jigen = ReplacePeriod(generalRegistrationEntry.WeekdayPeriod).ToString();
                var htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]/tr/td/table").SelectNodes("tr")[int.Parse(jigen)].SelectNodes("td")[int.Parse(youbi)].SelectSingleNode("table/tr[2]/td/a");
                if (htmlNode == null)
                {
                    Logger.Warn("Not found class in GeneralRegistrations.");
                    continue;
                }
                generalRegistrationEntry.EntriedKamokuCode = ReplaceJsArgs(htmlNode.Attributes["href"].Value, 1);
                generalRegistrationEntry.EntriedClassCode = ReplaceJsArgs(htmlNode.Attributes["href"].Value, 2);
            }
            foreach (var generalRegistrationEntry in generalRegistrationEntries)
            {
                SetGeneralRegistration(generalRegistrationEntry, false, out var result);
                if (result != 2 || !overwrite)
                    continue;
                var youbi = (ReplaceWeekday(generalRegistrationEntry.WeekdayPeriod) + 1).ToString();
                var jigen = ReplacePeriod(generalRegistrationEntry.WeekdayPeriod).ToString();
                SetGeneralRegistrationClear(youbi, jigen);
                SetGeneralRegistration(generalRegistrationEntry, false, out result);
                if (result != 0)
                    SetGeneralRegistration(generalRegistrationEntry, true, out _);
            }
            Logger.Info("End Set GeneralRegistrations.");
            SaveConfigs();
        }

        public void GetClassResults(out List<ClassResult> diffClassResults)
        {
            Logger.Info("Start Get ClassResults.");
            SetAcademicSystem(out _, out _, out _);
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/seisekiSearchStudentInit.do?mainMenuCode=008&parentMenuCode=007");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/seisekiSearchStudentInit.do?mainMenuCode=008&parentMenuCode=007");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            diffClassResults = new();
            if (htmlDocument.DocumentNode.SelectSingleNode("//table[@class=\"txt12\"]") == null)
            {
                Logger.Warn("Not found ClassResults list.");
                return;
            }
            if (htmlDocument.DocumentNode.SelectSingleNode("//table[@class=\"txt12\"]").SelectNodes("tr").Count - 1 > 0)
            {
                diffClassResults = new(SchoolGrade.ClassResults);
                SchoolGrade.ClassResults.Clear();
                Logger.Info($"Found {htmlDocument.DocumentNode.SelectSingleNode("//table[@class=\"txt12\"]").SelectNodes("tr").Count - 1} ClassResults.");
                for (var i = 1; i < htmlDocument.DocumentNode.SelectSingleNode("//table[@class=\"txt12\"]").SelectNodes("tr").Count; i++)
                {
                    var htmlNode = htmlDocument.DocumentNode.SelectSingleNode("//table[@class=\"txt12\"]").SelectNodes("tr")[i];
                    ClassResult classResult = new()
                    {
                        Subjects = htmlNode.SelectNodes("td")[0].InnerText.Trim(),
                        TeacherName = htmlNode.SelectNodes("td")[1].InnerText.Trim(),
                        SubjectsSection = htmlNode.SelectNodes("td")[2].InnerText.Trim(),
                        SelectionSection = htmlNode.SelectNodes("td")[3].InnerText.Trim(),
                        Credit = int.Parse(htmlNode.SelectNodes("td")[4].InnerText.Trim()),
                        Evaluation = htmlNode.SelectNodes("td")[5].InnerText.Trim()
                    };
                    if (htmlNode.SelectNodes("td")[6].InnerText.Trim() != "")
                        classResult.Score = double.Parse(htmlNode.SelectNodes("td")[6].InnerText.Trim());
                    if (htmlNode.SelectNodes("td")[7].InnerText.Trim() != "")
                        classResult.Gp = double.Parse(htmlNode.SelectNodes("td")[7].InnerText.Trim());
                    classResult.AcquisitionYear = htmlNode.SelectNodes("td")[8].InnerText.Trim();
                    classResult.ReportDate = DateTime.Parse(htmlNode.SelectNodes("td")[9].InnerText.Trim());
                    classResult.TestType = htmlNode.SelectNodes("td")[10].InnerText.Trim();
                    SchoolGrade.ClassResults.Add(classResult);
                }
                diffClassResults = SchoolGrade.ClassResults.Except(diffClassResults).ToList();
            }
            else
            {
                Logger.Warn("Not found ClassResults list.");
                return;
            }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/hyoukabetuTaniSearch.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/hyoukabetuTaniSearch.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            SchoolGrade.EvaluationCredits.Clear();
            for (var i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr").Count; i++)
                SchoolGrade.EvaluationCredits.Add(new() { Evaluation = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i].SelectNodes("td")[0].InnerText.Replace("\n", "").Replace("\t", ""), Credit = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i].SelectNodes("td")[1].InnerText) });
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/gpa.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/gpa.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            SchoolGrade.DepartmentGpa.Grade = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table/tr[1]/td[2]").InnerText.Replace("年", ""));
            SchoolGrade.DepartmentGpa.Gpa = double.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table/tr[2]/td[2]").InnerText);
            SchoolGrade.DepartmentGpa.SemesterGpas.Clear();
            for (var i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr").Count - 3; i++)
            {
                SemesterGpa semesterGpa = new()
                {
                    Year = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i + 2].SelectNodes("td")[0].InnerText.Split('　')[0].Replace("\n", "").Replace(" ", ""),
                    Semester = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i + 2].SelectNodes("td")[0].InnerText.Split('　')[1].Replace("\n", "").Replace(" ", ""),
                    Gpa = double.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i + 2].SelectNodes("td")[1].InnerText)
                };
                SchoolGrade.DepartmentGpa.SemesterGpas.Add(semesterGpa);
            }
            SchoolGrade.DepartmentGpa.CalculationDate = DateTime.ParseExact(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr").Last().SelectNodes("td")[1].InnerText, "yyyy年 MM月 dd日", null);
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/gpaImage.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/gpaImage.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            SchoolGrade.DepartmentGpa.DepartmentImage = Convert.ToBase64String(httpResponseMessage.Content.ReadAsByteArrayAsync().Result);
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/departmentGpa.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/departmentGpa.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            SchoolGrade.DepartmentGpa.DepartmentRank[0] = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[^2].SelectNodes("td")[1].InnerText.Trim(' ').Split('　')[1].Replace("位", ""));
            SchoolGrade.DepartmentGpa.DepartmentRank[1] = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[^2].SelectNodes("td")[1].InnerText.Trim(' ').Split('　')[0].Replace("人中", ""));
            SchoolGrade.DepartmentGpa.CourseRank[0] = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[^1].SelectNodes("td")[1].InnerText.Trim(' ').Split('　')[1].Replace("位", ""));
            SchoolGrade.DepartmentGpa.CourseRank[1] = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[^1].SelectNodes("td")[1].InnerText.Trim(' ').Split('　')[0].Replace("人中", ""));
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/departmentGpaImage.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/departmentGpaImage.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            SchoolGrade.DepartmentGpa.CourseImage = Convert.ToBase64String(httpResponseMessage.Content.ReadAsByteArrayAsync().Result);
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/nenbetuTaniSearch.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/nenbetuTaniSearch.do");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            SchoolGrade.YearCredits.Clear();
            for (var i = 1; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr").Count; i++)
                SchoolGrade.YearCredits.Add(new() { Year = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i].SelectNodes("td")[0].InnerText.Replace("\n", "").Trim(), Credit = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i].SelectNodes("td")[1].InnerText) });
            Account.ClassResultDateTime = DateTime.Now;
            Logger.Info("End Get ClassResults.");
            SaveConfigs();
        }

        public void GetClassTables()
        {
            Logger.Info("Start Get ClassTables.");
            if (!SetAcademicSystem(out _, out _, out _))
            {
                Logger.Warn("Not found AcademicSystem by overtime.");
                return;
            }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=005&parentMenuCode=004");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=005&parentMenuCode=004");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]") == null) { Logger.Warn("Return Get ClassTables by not found list."); return; }
            for (var i = 0; i < 7; i++)
            {
                if (ClassTables.Count < i + 1)
                    ClassTables.Add(new());
                for (var j = 0; j < 5; j++)
                {
                    var classTableCell = ClassTables[i][j];
                    var htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]/tr/td/table").SelectNodes("tr")[i + 1].SelectNodes("td")[j + 1].SelectSingleNode("table/tr[2]/td");
                    if (htmlNode == null)
                        classTableCell = new();
                    else if (htmlNode.SelectSingleNode("a") != null)
                    {
                        if ((htmlNode.InnerHtml.Contains("<font class=\"halfTime\">(前期前半)</font>") && semesterCode != 0) || (htmlNode.InnerHtml.Contains("<font class=\"halfTime\">(前期後半)</font>") && semesterCode != 1) || (htmlNode.InnerHtml.Contains("<font class=\"halfTime\">(後期前半)</font>") && semesterCode != 2) || (htmlNode.InnerHtml.Contains("<font class=\"halfTime\">(後期後半)</font>") && semesterCode != 3))
                            classTableCell = new();
                        else
                        {
                            if (htmlNode.SelectSingleNode("a") != null)
                            {
                                var detailKamokuCode =
                                    ReplaceJsArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 1);
                                var detailClassCode =
                                    ReplaceJsArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 2);
                                if (classTableCell.KamokuCode != detailKamokuCode ||
                                    classTableCell.ClassCode != detailClassCode)
                                {
                                    classTableCell = GetClassTableCell(detailKamokuCode, detailClassCode);
                                    classTableCell.ClassRoom = ReplaceSpace(htmlNode
                                        .InnerHtml[htmlNode.InnerHtml.LastIndexOf("<br>", StringComparison.Ordinal)..]
                                        .Replace("<br>", ""));
                                }
                            }
                        }
                    }
                    else if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]/tr/td/table").SelectNodes("tr")[i + 1].SelectNodes("td")[j + 1].SelectSingleNode("table[1]") != null && semesterCode is 0 or 2)
                    {
                        htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]/tr/td/table").SelectNodes("tr")[i + 1].SelectNodes("td")[j + 1].SelectSingleNode("table[1]").SelectSingleNode("tr/td");
                        if (htmlNode.SelectSingleNode("a") != null)
                        {
                            var detailKamokuCode =
                                ReplaceJsArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 1);
                            var detailClassCode =
                                ReplaceJsArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 2);
                            if (classTableCell.KamokuCode != detailKamokuCode ||
                                classTableCell.ClassCode != detailClassCode)
                            {
                                classTableCell = GetClassTableCell(detailKamokuCode, detailClassCode);
                                classTableCell.ClassRoom = ReplaceSpace(htmlNode
                                    .InnerHtml[htmlNode.InnerHtml.LastIndexOf("<br>", StringComparison.Ordinal)..]
                                    .Replace("<br>", ""));
                            }
                        }
                    }
                    else if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]/tr/td/table").SelectNodes("tr")[i + 1].SelectNodes("td")[j + 1].SelectSingleNode("table[2]") != null && semesterCode is 1 or 3)
                    {
                        htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]/tr/td/table").SelectNodes("tr")[i + 1].SelectNodes("td")[j + 1].SelectSingleNode("table[2]").SelectSingleNode("tr[3]/td");
                        if (htmlNode.SelectSingleNode("a") != null)
                        {
                            var detailKamokuCode =
                                ReplaceJsArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 1);
                            var detailClassCode =
                                ReplaceJsArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 2);
                            if (classTableCell.KamokuCode != detailKamokuCode ||
                                classTableCell.ClassCode != detailClassCode)
                            {
                                classTableCell = GetClassTableCell(detailKamokuCode, detailClassCode);
                                classTableCell.ClassRoom = ReplaceSpace(htmlNode
                                    .InnerHtml[htmlNode.InnerHtml.LastIndexOf("<br>", StringComparison.Ordinal)..]
                                    .Replace("<br>", ""));
                            }
                        }
                    }
                    ClassTables[i][j] = classTableCell;
                }
            }
            Logger.Info("End Get ClassTables.");
            ApplyReportsClassTables();
            ApplyQuizzesClassTables();
            SaveConfigs();
        }

        private ClassTableCell GetClassTableCell(string detailKamokuCode, string detailClassCode)
        {
            Logger.Info(
                $"Start Get ClassTableCell detailKamokuCode={detailKamokuCode}, detailClassCode={detailClassCode}.");
            ClassTableCell classTableCell = new();
            httpRequestMessage = new(new("GET"),
                $"https://gakujo.shizuoka.ac.jp/kyoumu/detailKamoku.do?detailKamokuCode={detailKamokuCode}&detailClassCode={detailClassCode}&gamen=jikanwari");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info(
                $"GET https://gakujo.shizuoka.ac.jp/kyoumu/detailKamoku.do?detailKamokuCode={detailKamokuCode}&detailClassCode={detailClassCode}&gamen=jikanwari");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            classTableCell.SubjectsName = ReplaceSpace(htmlDocument.DocumentNode
                .SelectSingleNode("//td[contains(text(), \"科目名\")]/following-sibling::td").InnerText);
            classTableCell.SubjectsId = ReplaceSpace(htmlDocument.DocumentNode
                .SelectSingleNode("//td[contains(text(), \"科目番号\")]/following-sibling::td").InnerText);
            classTableCell.ClassName = ReplaceSpace(htmlDocument.DocumentNode
                .SelectSingleNode("//td[contains(text(), \"クラス名\")]/following-sibling::td").InnerText);
            classTableCell.TeacherName = ReplaceSpace(htmlDocument.DocumentNode
                .SelectSingleNode("//td[contains(text(), \"担当教員\")]/following-sibling::td").InnerText);
            classTableCell.SubjectsSection = ReplaceSpace(htmlDocument.DocumentNode
                .SelectSingleNode("//td[contains(text(), \"科目区分\")]/following-sibling::td").InnerText);
            classTableCell.SelectionSection = ReplaceSpace(htmlDocument.DocumentNode
                .SelectSingleNode("//td[contains(text(), \"必修選択区分\")]/following-sibling::td").InnerText);
            classTableCell.Credit = int.Parse(htmlDocument.DocumentNode
                .SelectSingleNode("//td[contains(text(), \"単位数\")]/following-sibling::td").InnerText.Replace("\n", "")
                .Replace("\t", "").Replace("単位", ""));
            classTableCell.KamokuCode = detailKamokuCode;
            classTableCell.ClassCode = detailClassCode;
            Logger.Info(
                $"Start Get Syllabus schoolYear={schoolYear}, subjectCD={classTableCell.SubjectsId}, classCD={classTableCell.ClassCode}.");
            httpRequestMessage = new(new("GET"),
                $"https://gakujo.shizuoka.ac.jp/syllabus2/rishuuSyllabusSearch.do?schoolYear={schoolYear}&subjectCD={classTableCell.SubjectsId}&classCD={classTableCell.ClassCode}");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            Logger.Info($"GET https://gakujo.shizuoka.ac.jp/syllabus2/rishuuSyllabusSearch.do?schoolYear={schoolYear}&subjectCD={classTableCell.SubjectsId}&classCD={classTableCell.ClassCode}");
            Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"シラバスの詳細は以下となります。\")]") == null)
            {
                var subjectId = Regex.Match(htmlDocument.DocumentNode.SelectSingleNode("//td[contains(@onclick, \"dbLinkClick\")]").Attributes["onclick"].Value, "(?<=subjectID=)\\d*").Value;
                var formatCd = Regex.Match(htmlDocument.DocumentNode.SelectSingleNode("//td[contains(@onclick, \"dbLinkClick\")]").Attributes["onclick"].Value, "(?<=formatCD=)\\d*").Value;
                httpRequestMessage = new(new("GET"),
                    $"https://gakujo.shizuoka.ac.jp/syllabus2/rishuuSyllabusDetailEdit.do?subjectID={subjectId}&formatCD={formatCd}&rowIndex=0&jikanwariSchoolYear={schoolYear}");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                Logger.Info($"GET https://gakujo.shizuoka.ac.jp/syllabus2/rishuuSyllabusDetailEdit.do?subjectID={subjectId}&formatCD={formatCd}&rowIndex=0&jikanwariSchoolYear={schoolYear}");
                Logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            }
            classTableCell.Syllabus.SubjectsName = GetSyllabusValue(htmlDocument, "授業科目名");
            classTableCell.Syllabus.TeacherName = GetSyllabusValue(htmlDocument, "担当教員名");
            classTableCell.Syllabus.Affiliation = GetSyllabusValue(htmlDocument, "所属等");
            classTableCell.Syllabus.ResearchRoom = GetSyllabusValue(htmlDocument, "研究室");
            classTableCell.Syllabus.SharingTeacherName = GetSyllabusValue(htmlDocument, "分担教員名");
            classTableCell.Syllabus.ClassName = GetSyllabusValue(htmlDocument, "クラス");
            classTableCell.Syllabus.SemesterName = GetSyllabusValue(htmlDocument, "学期");
            classTableCell.Syllabus.SelectionSection = GetSyllabusValue(htmlDocument, "必修選択区分");
            classTableCell.Syllabus.TargetGrade = GetSyllabusValue(htmlDocument, "対象学年");
            classTableCell.Syllabus.Credit = GetSyllabusValue(htmlDocument, "単位数");
            classTableCell.Syllabus.WeekdayPeriod = GetSyllabusValue(htmlDocument, "曜日・時限");
            classTableCell.Syllabus.ClassRoom = GetSyllabusValue(htmlDocument, "教室");
            classTableCell.Syllabus.Keyword = GetSyllabusValue(htmlDocument, "キーワード");
            classTableCell.Syllabus.ClassTarget = GetSyllabusValue(htmlDocument, "授業の目標", true);
            classTableCell.Syllabus.LearningDetail = GetSyllabusValue(htmlDocument, "学習内容", true);
            classTableCell.Syllabus.ClassPlan = GetSyllabusValue(htmlDocument, "授業計画", true);
            classTableCell.Syllabus.Textbook = GetSyllabusValue(htmlDocument, "テキスト");
            classTableCell.Syllabus.ReferenceBook = GetSyllabusValue(htmlDocument, "参考書");
            classTableCell.Syllabus.PreparationReview = GetSyllabusValue(htmlDocument, "予習・復習について");
            classTableCell.Syllabus.EvaluationMethod = GetSyllabusValue(htmlDocument, "成績評価の方法･基準");
            classTableCell.Syllabus.OfficeHour = GetSyllabusValue(htmlDocument, "オフィスアワー");
            classTableCell.Syllabus.Message = GetSyllabusValue(htmlDocument, "担当教員からのメッセージ");
            classTableCell.Syllabus.ActiveLearning = GetSyllabusValue(htmlDocument, "アクティブ・ラーニング");
            classTableCell.Syllabus.TeacherPracticalExperience = GetSyllabusValue(htmlDocument, "実務経験のある教員の有無");
            classTableCell.Syllabus.TeacherCareerClassDetail = GetSyllabusValue(htmlDocument, "実務経験のある教員の経歴と授業内容");
            classTableCell.Syllabus.TeachingProfessionSection = GetSyllabusValue(htmlDocument, "教職科目区分");
            classTableCell.Syllabus.RelatedClassSubjects = GetSyllabusValue(htmlDocument, "関連授業科目");
            classTableCell.Syllabus.Other = GetSyllabusValue(htmlDocument, "その他");
            classTableCell.Syllabus.HomeClassStyle = GetSyllabusValue(htmlDocument, "在宅授業形態");
            classTableCell.Syllabus.HomeClassStyleDetail = GetSyllabusValue(htmlDocument, "在宅授業形態（詳細）");
            Logger.Info($"End Get Syllabus schoolYear={schoolYear}, subjectCD={classTableCell.SubjectsId}, classCD={classTableCell.ClassCode}.");
            Logger.Info($"End Get ClassTableCell detailKamokuCode={detailKamokuCode}, detailClassCode={detailClassCode}.");
            return classTableCell;
        }

        private static string GetSyllabusValue(HtmlDocument htmlDocument, string key, bool convert = false)
        {
            if (htmlDocument.DocumentNode.SelectSingleNode($"//font[contains(text(), \"{key}\")]/../following-sibling::td") == null)
                return "";
            string value;
            if (!convert)
                value = htmlDocument.DocumentNode.SelectSingleNode($"//font[contains(text(), \"{key}\")]/../following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Replace("&nbsp;", " ").Trim('　').Trim(' ');
            else
            {
                Config config = new()
                {
                    UnknownTags = Config.UnknownTagsOption.Bypass,
                    GithubFlavored = true,
                    RemoveComments = true,
                    SmartHrefHandling = true,
                };
                value = new Converter(config).Convert(htmlDocument.DocumentNode.SelectSingleNode($"//font[contains(text(), \"{key}\")]/../following-sibling::td").InnerHtml);
            }
            return Regex.Replace(Regex.Replace(value, @" +", " ").Replace("|\r\n\n \n |", "|\r\n|"), "(?<=[^|])\\r\\n(?=[^|])", "  \r\n");
        }
    }
}
