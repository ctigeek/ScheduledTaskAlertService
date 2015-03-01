using System.ServiceProcess;

namespace ScheduledTaskAlertService
{
    public partial class ScheduledTaskAlertWindowsService : ServiceBase
    {
        private readonly ActionManager actionManager;
        public ScheduledTaskAlertWindowsService()
        {
            InitializeComponent();
            actionManager = new ActionManager();
        }

        protected override void OnStart(string[] args)
        {
            actionManager.Start();
        }

        protected override void OnStop()
        {
            actionManager.Stop();
        }
    }
}
