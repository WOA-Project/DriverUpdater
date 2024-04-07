using System.ComponentModel;

namespace DriverUpdater
{
    public class Progress
    {
        private readonly BackgroundWorker worker = new();
        private readonly ProgressInterface progressclass;

        public Progress(DoWorkEventHandler DoWork, ProgressInterface progressclass)
        {
            this.progressclass = progressclass;
            worker.DoWork += DoWork;
            progressclass.Initialize();
            worker.RunWorkerAsync();
            progressclass.Show();
        }
    }
}
