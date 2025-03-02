using DialogHostAvalonia;

namespace DockMvvmSample.Services {
    public class DialogHostSingleton
    {
        private static DialogHost _instance;
        public static DialogHost Instance
        {
            get { return _instance ?? (_instance = new DialogHost()); }
        }
    }
}