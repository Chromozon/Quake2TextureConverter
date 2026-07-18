using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Windows;
using Prism.Ioc;
using Prism.Unity;

namespace Quake2TextureConverter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication
    {
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            //containerRegistry.Register<Services.ICustomerStore, Services.DbCustomerStore>();
            // register other needed services here
        }

        protected override Window CreateShell()
        {
            var window = Container.Resolve<MainWindow>();
            return window;
        }
    }
}
