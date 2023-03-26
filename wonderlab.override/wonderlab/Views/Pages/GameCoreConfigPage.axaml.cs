using Avalonia.Controls;
using MinecraftLaunch.Modules.Models.Launch;
using wonderlab.ViewModels.Pages;

namespace wonderlab.Views.Pages
{
    public partial class GameCoreConfigPage : UserControl {   

        public static GameCoreConfigPageViewModel ViewModel { get; set; }
        public GameCoreConfigPage() {       
            InitializeComponent();
            DataContext = ViewModel;
        }

        public GameCoreConfigPage(GameCore core) { 
            InitializeComponent();
            DataContext = ViewModel = new(core);
        }
    }
}
