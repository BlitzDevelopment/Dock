using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using System.Threading.Tasks;

namespace Blitz.Views
{
    public partial class IGenericDialogs
    {
        [RelayCommand]
        public async Task<bool> ShowWarning(string text)
        {
            var dialog = new MainGenericWarning(text);
            var result = await DialogHost.Show(dialog);
            var dialogIdentifier = result as string;
            dialog.DialogIdentifier = dialogIdentifier!;
            return result is bool isOkayPressed && isOkayPressed;
        }

        [RelayCommand]
        public async Task ShowError(string text)
        {
            var dialog = new MainGenericError(text);
            var result = await DialogHost.Show(dialog);
            var dialogIdentifier = result as string;
            dialog.DialogIdentifier = dialogIdentifier!;
            return;
        }
    }
}