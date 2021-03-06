using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Todoist.Net;
using Todoist.Net.Models;

namespace GakujoGUI
{
    internal class NotifyAPI
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private Tokens tokens = new();
        private TodoistClient? todoistClient;
        private DiscordSocketClient? discordSocketClient;
        private DateTime todoistUpdateDateTime = new();
        private Resources? todoistResources;

        private Resources TodoistResources
        {
            get
            {
                if (todoistUpdateDateTime + TimeSpan.FromMinutes(3) < DateTime.Now && todoistClient != null)
                {
                    todoistUpdateDateTime = DateTime.Now;
                    TodoistResources = todoistClient!.GetResourcesAsync().Result;
                }
                return todoistResources!;
            }
            set => todoistResources = value;
        }
        public Tokens Tokens { get => tokens; set => tokens = value; }

        private static string GetJsonPath(string value)
        {
            if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), assemblyName)))
            {
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), assemblyName));
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), @$"{assemblyName}\{value}.json");
        }

        private static readonly string assemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

        public NotifyAPI()
        {
            if (File.Exists(GetJsonPath("Tokens")))
            {
                Tokens = JsonConvert.DeserializeObject<Tokens>(File.ReadAllText(GetJsonPath("Tokens")))!;
                logger.Info("Load Tokens.");
            }
            Login();
        }

        public void SetTokens(string todoistToken, string discordChannel, string discordToken)
        {
            Tokens.TodoistToken = GakujoAPI.Protect(todoistToken, null!, DataProtectionScope.CurrentUser);
            Tokens.DiscordChannel = ulong.Parse(discordChannel);
            Tokens.DiscordToken = GakujoAPI.Protect(discordToken, null!, DataProtectionScope.CurrentUser);
            try { File.WriteAllText(GetJsonPath("Tokens"), JsonConvert.SerializeObject(Tokens, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save Tokens."); }
        }

        public void Login()
        {
            try
            {
                todoistClient = new(GakujoAPI.Unprotect(Tokens.TodoistToken, null!, DataProtectionScope.CurrentUser));
                TodoistResources = todoistClient!.GetResourcesAsync().Result;
                logger.Info("Login Todoist.");
            }
            catch (Exception exception) { logger.Error(exception, "Error Login Todoist."); }
            try
            {
                discordSocketClient = new();
                discordSocketClient.LoginAsync(TokenType.Bot, GakujoAPI.Unprotect(Tokens.DiscordToken, null!, DataProtectionScope.CurrentUser)).Wait();
                discordSocketClient.StartAsync().Wait();
                logger.Info("Login Discord.");
            }
            catch (Exception exception) { logger.Error(exception, "Error Login Discord."); }
        }

        #region Todoist

        private bool ExistsTodoistTask(string content, DateTime dateTime)
        {
            if (dateTime < DateTime.Now) { return true; }
            return TodoistResources.Items.Where(x => x.DueDate != null && x.Content == content && x.DueDate.Date == dateTime).Any();
        }

        private void AddTodoistTask(string content, DateTime dateTime)
        {
            try
            {
                if (!ExistsTodoistTask(content, dateTime))
                {
                    logger.Info($"Add Todoist task {todoistClient!.Items.AddAsync(new Item(content) { DueDate = new DueDate(dateTime.ToLocalTime()) }).Result}.");
                }
            }
            catch (Exception exception) { logger.Error(exception, "Error Add Todoist task."); }
        }

        private void ArchiveTodoistTask(string content, DateTime dateTime)
        {
            try
            {
                TodoistResources.Items.Where(x => x.DueDate != null && x.Content == content && x.DueDate.Date == dateTime && x.IsChecked != true).ToList().ForEach(x =>
                {
                    todoistClient!.Items.CloseAsync(x.Id).Wait();
                    logger.Info($"Archive Todoist task {x.Id}.");
                });
            }
            catch (Exception exception) { logger.Error(exception, "Error Archive Todoist task."); }
        }

        public void SetTodoistTask(List<Report> reports)
        {
            logger.Info("Start Set Todoist task reports.");
            if (TodoistResources == null) { logger.Warn("Return Set Todoist task reports by resource is null."); return; }
            reports.Where(x => x.IsSubmittable).ToList().ForEach(x => AddTodoistTask(x.ToShortString(), x.EndDateTime));
            reports.Where(x => !x.IsSubmittable).ToList().ForEach(x => ArchiveTodoistTask(x.ToShortString(), x.EndDateTime));
            logger.Info("End Set Todoist task reports.");
        }

        public void SetTodoistTask(List<Quiz> quizzes)
        {
            logger.Info("Start Set Todoist task quizzes.");
            if (TodoistResources == null) { logger.Warn("Return Set Todoist task quizzes by resource is null."); return; }
            quizzes.Where(x => x.IsSubmittable).ToList().ForEach(x => AddTodoistTask(x.ToShortString(), x.EndDateTime));
            quizzes.Where(x => !x.IsSubmittable).ToList().ForEach(x => ArchiveTodoistTask(x.ToShortString(), x.EndDateTime));
            logger.Info("End Set Todoist task quizzes.");
        }

        #endregion

        #region Discord

        public void NotifyDiscord(ClassContact classContact)
        {
            try
            {
                EmbedBuilder embedBuilder = new();
                embedBuilder.WithTitle(classContact.Title);
                embedBuilder.WithDescription(classContact.Content);
                embedBuilder.WithAuthor(classContact.Subjects);
                embedBuilder.WithTimestamp(classContact.ContactDateTime);
                (discordSocketClient!.GetChannel(Tokens.DiscordChannel) as IMessageChannel)!.SendMessageAsync(embed: embedBuilder.Build());
                logger.Info("Notify Discord ClassContact.");
            }
            catch (Exception exception) { logger.Error(exception, "Error Notify Discord ClassContact."); }
        }

        public void NotifyDiscord(Report report)
        {
            try
            {
                EmbedBuilder embedBuilder = new();
                embedBuilder.WithTitle(report.Title);
                embedBuilder.WithDescription($"{report.StartDateTime} -> {report.EndDateTime}");
                embedBuilder.WithAuthor(report.Subjects);
                embedBuilder.WithTimestamp(report.StartDateTime);
                (discordSocketClient!.GetChannel(Tokens.DiscordChannel) as IMessageChannel)!.SendMessageAsync(embed: embedBuilder.Build());
                logger.Info("Notify Discord Report.");
            }
            catch (Exception exception) { logger.Error(exception, "Error Notify Discord Report."); }
        }

        public void NotifyDiscord(Quiz quiz)
        {
            try
            {
                EmbedBuilder embedBuilder = new();
                embedBuilder.WithTitle(quiz.Title);
                embedBuilder.WithDescription($"{quiz.StartDateTime} -> {quiz.EndDateTime}");
                embedBuilder.WithAuthor(quiz.Subjects);
                embedBuilder.WithTimestamp(quiz.StartDateTime);
                (discordSocketClient!.GetChannel(Tokens.DiscordChannel) as IMessageChannel)!.SendMessageAsync(embed: embedBuilder.Build());
                logger.Info("Notify Discord Quiz.");
            }
            catch (Exception exception) { logger.Error(exception, "Error Notify Discord Quiz."); }
        }

        public void NotifyDiscord(ClassSharedFile classSharedFile)
        {
            try
            {
                EmbedBuilder embedBuilder = new();
                embedBuilder.WithTitle(classSharedFile.Title);
                embedBuilder.WithDescription(classSharedFile.Description);
                embedBuilder.WithAuthor(classSharedFile.Subjects);
                embedBuilder.WithTimestamp(classSharedFile.UpdateDateTime);
                (discordSocketClient!.GetChannel(Tokens.DiscordChannel) as IMessageChannel)!.SendMessageAsync(embed: embedBuilder.Build());
                logger.Info("Notify Discord ClassSharedFile.");
            }
            catch (Exception exception) { logger.Error(exception, "Error Notify Discord ClassSharedFile."); }
        }

        public void NotifyDiscord(ClassResult classResult, bool hideDetail)
        {
            try
            {
                EmbedBuilder embedBuilder = new();
                embedBuilder.WithTitle(classResult.Subjects);
                if (!hideDetail) { embedBuilder.WithDescription($"{classResult.Score} ({classResult.Evaluation})   {classResult.GP:F1}"); }
                embedBuilder.WithAuthor(classResult.TeacherName);
                embedBuilder.WithTimestamp(classResult.ReportDate);
                (discordSocketClient!.GetChannel(Tokens.DiscordChannel) as IMessageChannel)!.SendMessageAsync(embed: embedBuilder.Build());
                logger.Info("Notify Discord ClassResult.");
            }
            catch (Exception exception) { logger.Error(exception, "Error Notify Discord ClassResult."); }
        }

        #endregion

        #region LINE

        public void NotifyLINE(string content)
        {
            try
            {
                logger.Info("Notify LINE.");
                using HttpClient httpClient = new();
                HttpRequestMessage httpRequestMessage = new(new("POST"), "https://notify-api.line.me/api/notify");
                httpRequestMessage.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Tokens.LineToken}");
                httpRequestMessage.Content = new StringContent($"message={HttpUtility.UrlEncode(content, Encoding.UTF8)}");
                httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                HttpResponseMessage httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                logger.Info("POST https://notify-api.line.me/api/notify");
                logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                httpClient.Dispose();
            }
            catch (Exception exception) { logger.Error(exception, "Error Notify LINE."); }
        }

        #endregion
    }

    public class Tokens
    {
        public string TodoistToken { get; set; } = "";
        public ulong DiscordChannel { get; set; }
        public string DiscordToken { get; set; } = "";
        public string LineToken { get; set; } = "";
    }
}
