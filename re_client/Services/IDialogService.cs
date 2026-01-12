namespace WpfApp4.Services
{
    public interface IDialogService
    {
        void ShowWarning(string message, string title);
        void ShowError(string message, string title);
    }
}
