using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using log4net;
using log4net.Core;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json.Linq;
using ScheduledTaskAlertService.Models;
using Timer = System.Timers.Timer;

namespace ScheduledTaskAlertService
{
    internal class ActionManager
    {
        private static ILog log = LogManager.GetLogger(typeof(ActionManager));
        private static object lockObject = new object();
        private Timer timer;
        public bool Running { get; private set; }
        private readonly bool debug;
        private ScheduledTaskAlertServiceConfig currentConfig;
        private readonly int checkIntervalInSeconds;
        private readonly Dictionary<string, DateTime> lastCheckDateTime;

        public ActionManager()
        {
            lastCheckDateTime = new Dictionary<string, DateTime>();
            Running = false;
            checkIntervalInSeconds = int.Parse(ConfigurationManager.AppSettings["CheckIntervalInSeconds"]);
            timer = new Timer(1000*checkIntervalInSeconds);
            timer.Elapsed += timer_Elapsed;

            debug = ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level.Value <= Level.Debug.Value;
        }

        public void Start()
        {
            if (Running) throw new InvalidOperationException("ActionManager already running.");
            timer.Start();
            Running = true;
        }

        public void Stop()
        {
            lock (lockObject)
            {
                Running = false;
                if (timer.Enabled) timer.Stop();
            }
        }

        private void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (Monitor.TryEnter(lockObject))
            {
                try
                {
                    if (Running)
                    {
                        LoadServiceConfigFromFile();
                        foreach (var taskConfig in currentConfig.ScheduledTasks)
                        {
                            CheckScheduledTask(taskConfig);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex.ToString());
                }
                finally
                {
                    Monitor.Exit(lockObject);
                }
            }
        }

        private void CheckScheduledTask(ScheduledTaskConfig taskConfig)
        {
            try
            {
                if (debug)
                {
                    log.Debug(string.Format("Checking scheduled task {0} on machine {1}.", taskConfig.ScheduledTaskName, taskConfig.MachineName));
                }
                using (var ts = new TaskService(taskConfig.MachineName))
                {
                    var task = ts.FindTask(taskConfig.ScheduledTaskName, true);
                    if (task == null)
                    {
                        var errorString = string.Format("The scheduled task {0} on machine {1} could not be found.", taskConfig.ScheduledTaskName, taskConfig.MachineName);
                        throw new ConfigurationErrorsException(errorString);
                    }
                    if (task.State != TaskState.Running && GetLastCheckDateTime(taskConfig.ScheduledTaskName) < task.LastRunTime)
                    {
                        lastCheckDateTime[taskConfig.ScheduledTaskName] = DateTime.Now;
                        if (task.LastTaskResult != taskConfig.ExpectedResult)
                        {
                            var body = string.Format("The scheduled task '{0}' on machine '{1}' which ran on {2} did not exit with the expected result.\r\n It exited with '{3}'. It should have been '{4}'.",
                                taskConfig.ScheduledTaskName, taskConfig.MachineName, task.LastRunTime, task.LastTaskResult, taskConfig.ExpectedResult);
                            log.Error(body);
                            SendMailgunEmail("Scheduled Task '" + taskConfig.ScheduledTaskName + "' did not complete correctly.", body);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Error Checking scheduled task.", ex);
                SendMailgunEmail("Error checking scheduled task.", "The following error occured trying to check the status of a scheduled task: " + ex.ToString());
            }
        }

        private DateTime GetLastCheckDateTime(string taskName)
        {
            return (lastCheckDateTime.ContainsKey(taskName)) ? lastCheckDateTime[taskName] : DateTime.MinValue;
        }

        private void SendMailgunEmail(string subject, string body)
        {
            if (debug)
            {
                log.Debug(string.Format("Sending email:\r\n {0} \r\n {1}", subject, body));
            }
            var sendTo = currentConfig.EmailConfig.ToEmailAddressNotify.Split(',');
            var tagLine = string.Format("\r\n \r\n This email was generated at {0} from server {1}. ", DateTime.Now, Environment.MachineName);
            var formContentData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("from", currentConfig.EmailConfig.FromEmailAddress),
                new KeyValuePair<string, string>("subject", subject),
                new KeyValuePair<string, string>("text", body + tagLine)
            };

            formContentData.AddRange(sendTo.Select(s => new KeyValuePair<string, string>("to", s)).ToList());

            using (var httpclient = new HttpClient())
            {
                var byteArray = Encoding.ASCII.GetBytes("api:" + currentConfig.EmailConfig.MailGunApiKey);
                httpclient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                var request = new HttpRequestMessage(HttpMethod.Post, currentConfig.EmailConfig.MailGunApiUrl)
                {
                    Content = new FormUrlEncodedContent(formContentData.ToArray())
                };
                var response = httpclient.SendAsync(request).Result;
                if (!response.IsSuccessStatusCode)
                {
                    throw new ApplicationException("The call to mailgun returned " + response.StatusCode);
                }
            }
        }

        private void LoadServiceConfigFromFile()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var executingPath = executingAssembly.Location.Replace(executingAssembly.GetName().Name + ".exe", "");
            var configFilePath = ConfigurationManager.AppSettings["ScheduledTaskAlertServiceConfigFile"];
            var fullPath = Path.Combine(executingPath, configFilePath);
            string filebody;
            using (var reader = new StreamReader(fullPath))
            {
                filebody = reader.ReadToEnd();
            }
            var jobject = JObject.Parse(filebody);
            currentConfig = jobject.ToObject<ScheduledTaskAlertServiceConfig>();
        }
    }
}
