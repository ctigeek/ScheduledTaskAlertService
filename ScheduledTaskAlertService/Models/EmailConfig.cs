namespace ScheduledTaskAlertService.Models
{
    class EmailConfig
    {
        public string MailGunApiKey { get; set; }
        public string MailGunApiUrl { get; set; }
        public string FromEmailAddress { get; set; }
        public string ToEmailAddressNotify { get; set; }
    }
}
