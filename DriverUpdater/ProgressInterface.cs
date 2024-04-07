namespace DriverUpdater
{
    public interface ProgressInterface
    {
        void Initialize();
        void Show();
        void ReportProgress(int? ProgressPercentage, string StatusTitle, string StatusMessage);
        void Close();
    }
}
