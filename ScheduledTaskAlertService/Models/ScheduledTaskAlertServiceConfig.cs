namespace ScheduledTaskAlertService.Models
{
    class ScheduledTaskAlertServiceConfig
    {
        public EmailConfig EmailConfig { get; set; }
        public ScheduledTaskConfig[] ScheduledTasks { get; set; }
    }
}
