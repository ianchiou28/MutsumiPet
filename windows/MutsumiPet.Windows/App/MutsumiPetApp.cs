using System;
using System.Windows;
using MutsumiPet.Stores;
using MutsumiPet.Support;
using MutsumiPet.Views;

namespace MutsumiPet.App
{
    public static class MutsumiPetApp
    {
        [STAThread]
        public static int Main()
        {
            var application = new Application();
            application.ShutdownMode = ShutdownMode.OnLastWindowClose;

            var store = new PetStore(
                new FilePetSettings(),
                new DispatcherPetScheduler(application.Dispatcher));

            var window = new PetWindow(store);
            window.Show();

            return application.Run();
        }
    }
}
