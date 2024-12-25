using AvaloniaControls.Extensions;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace SnesConnectorApp.ViewModels;

public class ViewModelBase : ReactiveObject
{
    public ViewModelBase()
    {
        this.LinkProperties();
        
        PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != nameof(HasBeenModified))
            {
                HasBeenModified = true;
            }
        };
    }
    
    [Reactive] public bool HasBeenModified { get; set; }
}